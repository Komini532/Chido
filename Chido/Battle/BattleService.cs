using System.Numerics;
using Chido.Core;
using Chido.Core.Battle;
using Chido.Core.Battle.Actions;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Rewards;
using Chido.Core.World;
using Chido.Data;
using Chido.Data.Entities;
using Chido.Data.Loaders;
using Chido.Data.Locking;
using Chido.Data.Repositories;
using Chido.Rendering;
using Chido.Targeting;
using Microsoft.EntityFrameworkCore;

namespace Chido.Battle;

/// <summary>
/// 1回の戦闘行動の全体を1トランザクションに収める中核（戦闘システム 7.2・7.3）。
///
/// <para>
/// <b>コマンドは互いに独立した出来事であり、プロセス上の記憶を持たない。</b>
/// 毎回ここで DB から戦場を丸ごと復元し、ターンを解決し、書き戻してコミットする。
/// 途中で例外が飛べばターン全体が巻き戻り、不整合な状態は残らない。
/// </para>
/// <para>
/// 処理の順序は次の通り。アンカー行の用意だけがロックスコープの<b>外側</b>にあるのは、
/// スコープ内で <c>INSERT ... ON DUPLICATE</c> を打つと既存行に共有ロックが乗り、
/// 同一行を狙う2トランザクションが S ロックを持ち合ったままデッドロックになるため。
/// </para>
/// <code>
/// 1. プレイヤー行・チャンネル行の存在確認（ロックスコープの外側）
/// 2. ① プレイヤー行 → ② チャンネル行 をロック
/// 3. セッションの取得。無ければ生成し、出現中の敵を参加者として引き込む
/// 4. プレイヤーの参加（離脱済みなら再参加を拒否）
/// 5. 戦場の復元 → [対象] の解決 → 受理ゲート
/// 6. ターンの解決（または離脱・対象指定という別経路）
/// 7. 参加者状態・戦闘内効果の書き戻し、永続効果の減衰
/// 8. 終了判定 → 報酬 → 次の組の出現
/// 9. コミット
/// </code>
/// </summary>
public sealed class BattleService(
    IDbContextFactory<ChidoDbContext> dbFactory,
    GameCatalogs catalogs)
{
    /// <summary>ターンが開く行動か。セッション生成の契機はこれに限る（B-1）。</summary>
    private static bool OpensTurn(BattleActionKind kind)
        => kind is BattleActionKind.Attack or BattleActionKind.Skill
            or BattleActionKind.Defend or BattleActionKind.Use;

    public async Task<BattleActionOutcome> ExecuteAsync(
        BattleActionRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // 1. アンカー行の用意・存在確認
        await new PlayerRepository(db).EnsureAsync(request.UserId, request.UserName, cancellationToken);

        var channels = new ChannelStateRepository(db);

        if (await channels.FindAsync(request.ChannelId, cancellationToken) is null)
        {
            return Reject("このチャンネルは戦闘チャンネルではありません。先に `/battle-init` を実行してください。");
        }

        var rng = new Random();
        var message = new BattleMessage();

        // 2. 正準ロック順序 ① → ②。②を保持している間、同一チャンネルの戦闘行動は完全に直列化される
        await using var scope = await BattleLock.BeginAsync(db, cancellationToken);
        await scope.LockPlayerAsync(request.UserId, cancellationToken);
        var channel = await scope.LockChannelAsync(request.ChannelId, cancellationToken);

        var sessions = new BattleSessionRepository(db);
        var record = await sessions.FindActiveAsync(request.ChannelId, cancellationToken);

        // 3. セッションの取得・生成
        if (record is null)
        {
            if (!OpensTurn(request.Kind))
            {
                return Reject("このチャンネルではまだ戦闘が始まっていません。");
            }

            record = await StartSessionAsync(db, channel, request, rng, cancellationToken);

            if (record is null)
            {
                return Reject("このチャンネルに敵が出現していません。`/battle-init` を実行してください。");
            }
        }

        // 4. 参加。離脱後は同じ戦闘に再参加できない（B-13・戦闘システム 4.3）
        var existing = await sessions.FindParticipantAsync(
            record.SessionId, request.UserId, cancellationToken);

        if (existing is { Status: ParticipantStatus.Escaped })
        {
            return Reject("この戦闘からは既に離脱しています。次の戦闘を待ってください。");
        }

        if (existing is null)
        {
            BattleParticipantRecord joined;

            try
            {
                joined = await sessions.JoinPlayerAsync(
                    record.SessionId, request.UserId,
                    await sessions.NextPlayerDisplayOrderAsync(record.SessionId, cancellationToken),
                    cancellationToken);
            }
            catch (SingleSessionViolationException)
            {
                return Reject("既に別の戦闘に参加しています。そちらを終えるか `/escape` してください。");
            }

            // 参加時は全快で初期化する（戦闘システム 3.4）。最大HPは装備と状態変化から
            // 毎回動的に算出されるため、実体を組み立てるまで確定しない。
            // ここを省くと参加者は現在HP0で戦場に出て、最初の反撃でそのまま戦闘不能になる
            var entity = await new PlayerLoader(db, catalogs.Effects)
                .LoadAsync(request.UserId, joined.EntityId, cancellationToken);

            joined.CurrentHp = entity.MaxLife;
            await db.SaveChangesAsync(cancellationToken);
        }

        // 5. 戦場の復元
        var session = await new BattleStateLoader(db, catalogs.Effects, catalogs.World)
            .LoadAsync(record, cancellationToken);

        var actor = session.Participants.FirstOrDefault(p => p.DiscordUserId == request.UserId);

        if (actor is null)
        {
            return Reject("参加者として復元できませんでした。マスタの投入状況を確認してください。");
        }

        var resolution = ResolveTarget(session, request.TargetInput);

        if (resolution.Status is TargetResolutionStatus.Ambiguous or TargetResolutionStatus.Unresolved)
        {
            return Reject(resolution.Message!);
        }

        var commandTarget = resolution.Participant;

        // 6. 経路の分岐
        var step = request.Kind switch
        {
            BattleActionKind.Target => ApplyTarget(session, actor, commandTarget, message),
            BattleActionKind.Escape => await ApplyEscapeAsync(session, actor, message),
            _ => await ApplyTurnAsync(db, session, actor, commandTarget, request, rng, message, cancellationToken),
        };

        if (step.Refusal is { } stepRefusal) return Reject(stepRefusal);

        // 7. 書き戻し
        await SaveAsync(db, session, step.DecayedUsers, cancellationToken);

        // 8. 終了判定
        var (ended, reason) = session.CheckEndCondition();

        if (ended)
        {
            session.Finish(reason);
            await ConcludeAsync(db, channel, session, record, reason, rng, message, cancellationToken);
        }

        // 9. コミット
        await scope.CommitAsync(cancellationToken);

        return new BattleActionOutcome(
            true, message.Render(catalogs.EffectNameOf), ended ? reason : null);
    }

    /// <summary>
    /// セッションを生成し、チャンネルに出現中の敵を参加者として引き込む。
    ///
    /// <para>
    /// <b>敵の auto 付与はここで行う。</b>出現の時点では参加者行が無く、
    /// <c>chido_battle_effect.entity_id</c> が参照すべき識別子そのものが存在しないため、
    /// 出現時に付与しても書き出す先が無い（<c>chido_enemy_effects_master</c> の
    /// 「実際の付与インスタンスは戦闘開始時に書き込まれる」という記述と一致する）。
    /// 出現からここまでの間に敵を観測する経路が無いため、抽選の時点が動いても
    /// 外から見える挙動は変わらない。
    /// </para>
    /// <para>
    /// 全快は装備と状態変化を載せた後に行う。最大HPは動的算出であり、先に全快させると
    /// 装備ぶんが乗る前の値で現在HPが固定される（クランプしない設計のため以後も是正されない）。
    /// </para>
    /// </summary>
    /// <returns>出現中の敵が1体もいなければ null。</returns>
    private async Task<BattleSessionRecord?> StartSessionAsync(
        ChidoDbContext db,
        ChannelStateRecord channel,
        BattleActionRequest request,
        Random rng,
        CancellationToken cancellationToken)
    {
        var current = await new ChannelEnemyRepository(db).LoadAsync(request.ChannelId, cancellationToken);

        if (current.Count == 0) return null;

        var sessions = new BattleSessionRepository(db);

        var record = await sessions.CreateAsync(
            request.GuildId, request.ChannelId, cancellationToken: cancellationToken);

        // 参加者行を先に作り、entity_id を確定させる。実体はその識別子で組み立てる
        // （CurrentTarget と台帳の帰属、そして付与インスタンスの保持者が同じIDで解決されるため）
        var rows = new List<BattleParticipantRecord>();

        foreach (var enemy in current)
        {
            rows.Add(await sessions.JoinEnemyAsync(
                record.SessionId, enemy.EnemyId, enemy.SpawnIndex,
                initialTp: 0, currentHp: BigInteger.Zero, cancellationToken));
        }

        var entityIds = rows.ToDictionary(x => x.EnemyId!.Value, x => x.EntityId);

        var enemies = await new EnemyLoader(db, catalogs.World)
            .LoadAsync(current.Select(x => x.EnemyId).ToList(), entityIds, cancellationToken);

        var applier = catalogs.NewApplier();
        var battleEffects = new BattleEffectRepository(db);

        foreach (var enemy in enemies)
        {
            foreach (var auto in catalogs.World.AutoEffectsOf(enemy.MasterKey))
            {
                if (!auto.GrantRate.Roll(rng)) continue;

                applier.GrantAuto(
                    enemy, EntityType.Enemy, auto.EffectKey,
                    auto.EffectRate, auto.AttackType, auto.DurationActions);
            }

            enemy.RestoreToFull();

            var row = rows.First(x => x.EntityId == enemy.Id);

            var participant = new BattleParticipant(
                enemy, EntityType.Enemy,
                enemyId: row.EnemyId, displayOrder: row.DisplayOrder, initialTp: enemy.InitialTp);

            await battleEffects.ReplaceAsync(participant, cancellationToken);
            await sessions.SaveParticipantStateAsync(participant, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return record;
    }

    /// <summary>
    /// <c>[対象]</c> を解決する。候補は<b>離脱していない参加者</b>。
    ///
    /// <c>Defeated</c> を候補に残すのは、受理ゲート（<c>ValidateCommandTarget</c>）が
    /// 「既にやられています」という具体的な理由を返せるようにするため。候補から外すと
    /// 「見つかりません」に化けて、指定が誤りだったのか対象が倒れたのかが区別できなくなる。
    /// </summary>
    private static TargetResolutionResult ResolveTarget(BattleSession session, string? input)
        => TargetResolution.Resolve(
            input,
            session.Participants.Where(p => p.Status != ParticipantStatus.Escaped).ToList());

    /// <summary>
    /// <c>/target</c>。次の行動の宛先を決めるだけで、ターンも反撃も発生しない（B-1）。
    ///
    /// <c>CurrentTarget</c> は定義上「相対する側の1体」であるため、味方が解決された場合は
    /// 指定として成立しない。<c>[対象]</c> は敵・味方の双方を受理する単一の引数だが（9.2）、
    /// 味方の指定が意味を持つのは <c>target_rule = 味方</c> のモーションを持つスキルの中だけである。
    /// </summary>
    private static StepResult ApplyTarget(
        BattleSession session, BattleParticipant actor, BattleParticipant? target, BattleMessage message)
    {
        if (target is null)
        {
            return StepResult.Refuse("対象を指定してください。");
        }

        if (target.EntityType == actor.EntityType)
        {
            return StepResult.Refuse($"{target.Entity.Name} は味方です。`/target` には敵を指定してください。");
        }

        if (TurnResolver.ValidateCommandTarget(target) is { } refusal)
        {
            return StepResult.Refuse(refusal);
        }

        if (!session.SetTarget(actor, target.Entity.Id))
        {
            return StepResult.Refuse($"{target.Entity.Name} は対象にできません。");
        }

        message.AddLogs([$"{actor.Entity.Name} は {target.Entity.Name} に狙いを定めた。"]);
        message.MarkChanged(actor, target);

        // ターンが開いていないため減衰は起こらない
        return StepResult.Accept();
    }

    /// <summary>
    /// <c>/escape</c>。ターンを消費しないため反撃も <c>SlipDamage</c> も減衰も起こらない。
    ///
    /// <b>単一セッション制約の拘束はここでしか自力では解けない</b>（戦闘システム 4.3）。
    /// 参加中セッションの記録を外すのを忘れると、離脱したはずのプレイヤーが
    /// そのセッションが終わるまで他の戦闘に参加できないままになる。
    /// </summary>
    private static async Task<StepResult> ApplyEscapeAsync(
        BattleSession session, BattleParticipant actor, BattleMessage message)
    {
        var result = await new EscapeAction().ExecuteAsync(
            actor, session.Participants, session, Random.Shared);

        message.AddLogs(result.LogEntries);
        message.MarkChanged(actor);

        // 行動枠そのものが開かないため、反撃も SlipDamage も減衰も起こらない
        return StepResult.Accept();
    }

    /// <summary>
    /// ターンが開く行動（攻撃・スキル・防御・アイテム）。
    /// </summary>
    private async Task<StepResult> ApplyTurnAsync(
        ChidoDbContext db,
        BattleSession session,
        BattleParticipant actor,
        BattleParticipant? commandTarget,
        BattleActionRequest request,
        Random rng,
        BattleMessage message,
        CancellationToken cancellationToken)
    {
        if (!actor.IsActive)
        {
            return StepResult.Refuse("戦闘不能のため行動できません。`/escape` で戦闘から離脱できます。");
        }

        var selection = await SelectSkillAsync(db, actor, request, cancellationToken);
        if (selection.Refusal is { } refusal) return StepResult.Refuse(refusal);

        var skill = selection.Skill!;

        if (!actor.CanAfford(skill.RequireTp))
        {
            return StepResult.Refuse($"TPが足りません（必要 {skill.RequireTp} / 現在 {actor.CurrentTp}）。");
        }

        // 受理ゲート。ここで拒否された行動は不成立であり、ターン・TP・反撃・減衰のいずれも発生しない
        if (TurnResolver.ValidateCommandTarget(commandTarget) is { } targetRefusal)
        {
            return StepResult.Refuse(targetRefusal);
        }

        // 空振りの通知。入力が結果に反映されなかったことは必ず伝える（戦闘システム 4.2）
        message.AddNotice(TurnResolver.DetectAllyTargetMiss(actor, skill, commandTarget));

        var applier = catalogs.NewApplier();
        var resolver = new TurnResolver(new SkillPlayer(applier));
        var selector = new EnemySkillSelector(catalogs.Skills.Attack);

        var result = resolver.Resolve(
            actor, skill, session, rng,
            counterSkillSelector: counter => selector.Select(counter, rng),
            commandTarget: commandTarget,
            enemyAllySelector: new AllyTargetResolver(session, rng).AsSelector(),
            onDamageDealt: session.RecordDamageDealt);

        session.RecordAction();

        message.AddLogs(result.Logs);

        // そのターンで状態が変化したエンティティ＝関与者集合（行動者＋反撃者）と、
        // 味方対象モーションで影響を受けた1体。構造上この3体に収まる
        message.MarkChanged(
            result.First.Participant, result.Second.Participant, actor, commandTarget);

        // 消費した使い切りアイテムはターンが成立した後で減らす。受理ゲートで弾かれた行動では
        // 減らしてはならない（不成立の行動が資源だけを奪うことになる）
        if (selection.ConsumeItemKey is { } itemKey)
        {
            await new InventoryRepository(db).ConsumeAsync(actor.DiscordUserId!.Value, itemKey, cancellationToken);
        }

        // 減衰の対象は関与者集合（ターン開始時に固定される行動者＋反撃者の2体）だけ。
        // メモリ上の効果は EffectDecay が既に消費しているため、ここでは
        // chido_player_effect 側の行を同じ集合ぶんだけ消費させる
        var decayed = new[] { result.First.Participant, result.Second.Participant }
            .Select(p => p.DiscordUserId)
            .OfType<ulong>()
            .ToHashSet();

        return StepResult.Accept(decayed);
    }

    /// <summary>実行するスキルを決める。受理できない場合は理由を返す。</summary>
    private async Task<SkillSelection> SelectSkillAsync(
        ChidoDbContext db,
        BattleParticipant actor,
        BattleActionRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.Kind)
        {
            case BattleActionKind.Attack:
                return new SkillSelection(catalogs.Skills.Attack);

            case BattleActionKind.Defend:
                return new SkillSelection(catalogs.Skills.Defend);

            case BattleActionKind.Skill:
            {
                if (request.SkillKey is not { Length: > 0 } skillKey)
                    return SkillSelection.Refuse("スキルを指定してください。");

                if (catalogs.Skills.Find(skillKey) is not { } skill)
                    return SkillSelection.Refuse($"{skillKey} というスキルは存在しません。");

                // 通常攻撃・防御は習得管理の対象外であり、専用コマンドから使う
                if (skillKey == GameConstants.AttackSkillKey || skillKey == GameConstants.DefendSkillKey)
                    return SkillSelection.Refuse($"{skill.Name} は専用のコマンドから使用してください。");

                var learned = await new InventoryRepository(db)
                    .LearnedSkillsAsync(actor.DiscordUserId!.Value, cancellationToken);

                return learned.Contains(skillKey)
                    ? new SkillSelection(skill)
                    : SkillSelection.Refuse($"{skill.Name} をまだ習得していません。");
            }

            case BattleActionKind.Use:
            {
                if (request.ItemKey is not { Length: > 0 } itemKey)
                    return SkillSelection.Refuse("アイテムを指定してください。");

                var inventory = new InventoryRepository(db);
                var owned = await inventory.OwnedItemsAsync(actor.DiscordUserId!.Value, cancellationToken);

                if (owned.FirstOrDefault(x => x.ItemKey == itemKey) is not { Quantity: > 0 } item)
                    return SkillSelection.Refuse("そのアイテムを所持していません。");

                if (await inventory.UsedSkillKeyAsync(itemKey, cancellationToken) is not { } skillKey)
                    return SkillSelection.Refuse($"{item.Name} は戦闘中に使用できません。");

                // アイテムからの発動は習得状況を問わない
                if (catalogs.Skills.Find(skillKey) is not { } skill)
                    return SkillSelection.Refuse($"{item.Name} の効果が定義されていません。");

                return new SkillSelection(skill, item.IsConsumable ? itemKey : null);
            }

            default:
                return SkillSelection.Refuse("この行動はターンを開きません。");
        }
    }

    /// <summary>
    /// 参加者の可変状態と戦闘内効果を書き戻し、永続効果をその場で減衰させる。
    ///
    /// <para>
    /// 減衰の対象は<b>関与者集合のプレイヤー</b>のみ。集合外のプレイヤーの永続効果は
    /// 動かない（戦闘システム 5.4）。関与者集合はメモリ上では
    /// <c>EffectDecay</c> が既に処理しているため、ここでは同じ集合に対して
    /// <c>chido_player_effect</c> 側の行を1つ消費させる。
    /// </para>
    /// <para>
    /// 書き戻す参加者を関与者集合に絞らないのは、味方対象モーション（回復・バフ）で
    /// 集合外の参加者が変化しうるため。全参加者を書き戻せば、どのモーションが誰に
    /// 作用したかを呼び出し側が知らなくてよい。
    /// </para>
    /// </summary>
    private static async Task SaveAsync(
        ChidoDbContext db,
        BattleSession session,
        IReadOnlySet<ulong> decayed,
        CancellationToken cancellationToken)
    {
        var sessions = new BattleSessionRepository(db);
        var battleEffects = new BattleEffectRepository(db);
        var playerEffects = new PlayerEffectRepository(db);

        foreach (var participant in session.Participants)
        {
            await sessions.SaveParticipantStateAsync(participant, cancellationToken);
            await battleEffects.ReplaceAsync(participant, cancellationToken);

            if (participant.DiscordUserId is not { } userId) continue;

            // 離脱した参加者の拘束を解く。/escape だけでなく離脱モーションで離脱させられた
            // 場合も同じ状態になるため、経路ではなく結果の状態を見て判定する
            if (participant.Status == ParticipantStatus.Escaped)
            {
                await sessions.LeaveAsync(userId, cancellationToken);
            }

            // 永続効果の減衰。対象は関与者集合のプレイヤーのみ
            if (decayed.Contains(userId))
            {
                await playerEffects.DecayAsync(userId, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// セッションの終了処理。終了 → 報酬 → 次の組の出現、までを1つのトランザクション内で行う。
    ///
    /// 報酬の適用が別トランザクションに分かれると、途中で失敗したときに
    /// 「敵は倒れたが報酬が入っていない」状態が残る。
    /// </summary>
    private async Task ConcludeAsync(
        ChidoDbContext db,
        ChannelStateRecord channel,
        BattleSession session,
        BattleSessionRecord record,
        BattleEndReason reason,
        Random rng,
        BattleMessage message,
        CancellationToken cancellationToken)
    {
        var enemies = session.Participants.Where(p => !p.IsPlayer).ToList();

        await GrantRewardsAsync(db, session, enemies, reason, rng, message, cancellationToken);

        // 戦闘内スコープの効果はここで落ちる。永続スコープには触れない
        await new BattleEffectRepository(db).ClearAsync(
            session.Participants.Select(p => p.Entity.Id).ToList(), cancellationToken);

        await new BattleSessionRepository(db).FinishAsync(record.SessionId, reason, cancellationToken);

        await SpawnNextAsync(db, channel, reason, rng, message, cancellationToken);
    }

    private async Task GrantRewardsAsync(
        ChidoDbContext db,
        BattleSession session,
        IReadOnlyList<BattleParticipant> enemies,
        BattleEndReason reason,
        Random rng,
        BattleMessage message,
        CancellationToken cancellationToken)
    {
        if (reason != BattleEndReason.PlayerVictory) return;

        var rewards = new RewardRepository(db);
        var enemyLoader = new EnemyLoader(db, catalogs.World);

        var defeated = enemies
            .Where(p => p.Status == ParticipantStatus.Defeated && p.EnemyId is not null)
            .Select(p => p.EnemyId!.Value)
            .ToList();

        // 分母は組の全メンバーの出現時MaxLifeの合計。撃破されなかったメンバー
        // （逃走した敵）も「倒すために必要だった仕事量」には含める
        var spawnMaxLifeSum = await enemyLoader.SpawnMaxLifeSumAsync(
            enemies.Where(p => p.EnemyId is not null).Select(p => p.EnemyId!.Value).ToList(),
            cancellationToken);

        var contributions = session.Participants
            .Where(p => p.IsPlayer && p.DiscordUserId is not null)
            .Select(p => new PlayerContribution(
                p.DiscordUserId!.Value,
                p.TotalDamageDealt,
                p.Entity.Luck,
                p.Status == ParticipantStatus.Escaped))
            .ToList();

        var enemyLevel = enemies.Count == 0 ? BigInteger.Zero : enemies[0].Entity.Level;

        var context = await rewards.BuildContextAsync(
            contributions, enemyLevel, spawnMaxLifeSum, defeated, cancellationToken);

        var granted = RewardCalculator.Calculate(reason, context, rng);

        await rewards.ApplyAsync(granted, cancellationToken);

        var defeatedKeys = enemies
            .Where(p => p.Status == ParticipantStatus.Defeated)
            .Select(p => (p.Entity as Core.Entities.Enemies.Enemy)?.MasterKey)
            .OfType<string>()
            .ToHashSet();

        foreach (var reward in granted)
        {
            message.AddLogs([$"<@{reward.UserId}> は経験値 {reward.Exp} と {reward.Currency} を得た。"]);

            // 称号は報酬を適用した後の状態で判定する
            // （撃破で得た経験値・通貨・アイテムがそのターンの条件を満たしうるため）
            var level = LevelOf(session, reward.UserId);

            foreach (var title in await rewards.GrantTitlesAsync(
                         reward.UserId, level, defeatedKeys, cancellationToken))
            {
                message.AddLogs([$"<@{reward.UserId}> は称号「{title}」を獲得した。"]);
            }
        }
    }

    /// <summary>
    /// 次の組の出現。<c>ChannelMissing</c> のときは呼ばれない
    /// （チャンネルの永続状態ごと削除するため。戦闘システム 6.3）。
    /// </summary>
    private async Task SpawnNextAsync(
        ChidoDbContext db,
        ChannelStateRecord channel,
        BattleEndReason reason,
        Random rng,
        BattleMessage message,
        CancellationToken cancellationToken)
    {
        if (reason == BattleEndReason.ChannelMissing) return;

        var plan = SpawnPlanner.PlanNext(
            reason,
            channel.CurrentFieldKey,
            channel.CumulativeEnemyLevel,
            // 初期化直後に組が記録されていない状態は正常系に無いが、抽選を止めるほどではない。
            // 空の組キーは DrawGroup を経由する分岐へ落ちるだけで済む
            channel.CurrentGroupKey ?? string.Empty,
            channel.CurrentRarity ?? Rarity.Common,
            catalogs.World,
            rng);

        await ApplyPlanAsync(db, channel.ChannelId, plan, rng, message, cancellationToken);
    }

    /// <summary>
    /// 戦闘チャンネルとして初期化し、最初の組を出現させる（戦闘システム 10.5）。
    ///
    /// <para>
    /// 組の抽選と生成は <c>PlayerVictory</c> 時と<b>同一のロジック</b>だが、
    /// <b>レベル加算とフィールド切替判定は行わない</b>（累積敵レベルは1で固定、フィールドは草原固定）。
    /// </para>
    /// <para>
    /// <b>冪等ではない。</b>「既にあるものを消去して作り直す」機能を伴わないため、
    /// 初期化済みのチャンネルでの再実行は失敗する。黙って作り直せるようにすると、
    /// 進行中の戦闘と累積敵レベルを取り違えて消す事故が起きうる。
    /// </para>
    /// <para>
    /// <b>セッションはここでは生成しない。</b>セッションはプレイヤーの最初の戦闘行為時に生成される。
    /// したがって初期化直後は「セッションに属さない、チャンネルに出現中の敵」が存在する状態になる。
    /// </para>
    /// </summary>
    public async Task<BattleActionOutcome> InitializeChannelAsync(
        ulong channelId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // アンカー行の作成はロックスコープの外側で行う
        if (!await new ChannelStateRepository(db).EnsureAsync(channelId, cancellationToken))
        {
            return Reject("このチャンネルは既に戦闘チャンネルとして初期化されています。");
        }

        var rng = new Random();
        var message = new BattleMessage();

        await using var scope = await BattleLock.BeginAsync(db, cancellationToken);
        await scope.LockChannelAsync(channelId, cancellationToken);

        await ApplyPlanAsync(
            db, channelId, SpawnPlanner.PlanInitial(catalogs.World, rng), rng, message, cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return new BattleActionOutcome(true, message.Render(catalogs.EffectNameOf));
    }

    /// <summary>計画に沿って組を生成し、チャンネルへ記録する。初期化と次の出現で共用する。</summary>
    private async Task ApplyPlanAsync(
        ChidoDbContext db,
        ulong channelId,
        SpawnPlan plan,
        Random rng,
        BattleMessage message,
        CancellationToken cancellationToken)
    {
        // auto 付与はセッション開始時に行うため、ここでは適用しない（StartSessionAsync 参照）
        var spawned = new GroupSpawner(catalogs.World).Spawn(plan.GroupKey, plan.CumulativeEnemyLevel, rng);

        await new ChannelEnemyRepository(db).ReplaceAsync(channelId, spawned, cancellationToken);
        await new ChannelStateRepository(db).ApplyPlanAsync(channelId, plan, cancellationToken);

        message.AddLogs([$"{string.Join("・", spawned.Select(s => s.Enemy.Name))} が現れた！"]);

        // 縮退はマスタの不整合を意味する。文字数予算の削減対象から外して必ず届ける
        if (plan.GroupDegraded)
        {
            message.DegradationNotices.Add("敵の組を抽選できなかったため、草原の Common へ縮退しました。");
        }

        if (plan.FieldDegraded)
        {
            message.DegradationNotices.Add("遷移先が定義されていないため、草原へ縮退しました。");
        }
        else if (plan.FieldChanged)
        {
            message.DegradationNotices.Add($"フィールドが {plan.FieldKey} に変わりました。");
        }
    }

    private static BigInteger LevelOf(BattleSession session, ulong userId)
        => session.Participants
            .FirstOrDefault(p => p.DiscordUserId == userId)?.Entity.Level ?? BigInteger.One;

    private static BattleActionOutcome Reject(string reason)
        => new(false, new RenderedBattleMessage([], [], [reason]));

    /// <summary>実行するスキルの決定結果。</summary>
    private readonly record struct SkillSelection(
        Skill? Skill, string? ConsumeItemKey = null, string? Refusal = null)
    {
        public static SkillSelection Refuse(string reason) => new(null, null, reason);
    }

    /// <summary>
    /// 経路ごとの処理結果。
    /// </summary>
    /// <param name="Refusal">
    /// 不成立の理由。null なら成立。不成立の場合、ターン・TP・反撃・減衰のいずれも発生しておらず、
    /// トランザクションはコミットされずに巻き戻る。
    /// </param>
    /// <param name="DecayedUsers">
    /// 永続効果を減衰させるプレイヤー（関与者集合）。ターンが開かない経路では空になる。
    /// </param>
    private readonly record struct StepResult(string? Refusal, IReadOnlySet<ulong> DecayedUsers)
    {
        public static StepResult Refuse(string reason) => new(reason, new HashSet<ulong>());

        public static StepResult Accept(IReadOnlySet<ulong>? decayed = null)
            => new(null, decayed ?? new HashSet<ulong>());
    }
}
