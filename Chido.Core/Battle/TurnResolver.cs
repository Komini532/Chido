using System;
using System.Collections.Generic;
using System.Numerics;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;

namespace Chido.Core.Battle;

/// <summary>
/// 1ターンの骨格（戦闘システム 4.1・4.2・5.4）。
///
/// プレイヤーと敵は互いに「1スキルに対し1スキル」で応酬する。処理順は
/// 「行動側スキルの全モーション再生 → 相手側スキルの全モーション再生」という直列であり、
/// 並行処理は行わない。1スキルが複数モーションを持つ場合、そのモーション集合は
/// 「1スキルの効果」として上から順に再生される。
///
/// <b>先攻側の一撃で後攻側が戦闘不能になった場合、後攻側の行動はキャンセルされる</b>
/// （＝ダメージ計算自体が行われない）。高 Priority の攻撃スキルはこれを能動的に狙う手段になる。
///
/// 状態変化を含めた1ターンの処理順は次の通り（戦闘システム 5.4「行動不能ターンの処理順」）。
/// <code>
/// 1. 両者のスキル確定（敵は action_pattern_type で抽選、require_tp フォールバック含む）
/// 2. Priority → Speed → Random で行動順決定
/// 3. 先攻: DisableMove 判定 → 成立ならモーション再生をスキップ / 不成立なら再生
/// 4. 先攻の SlipDamage 発動
/// 5. 後攻: 同様（先攻の一撃で戦闘不能なら 4.1 によりキャンセル）
/// 6. 後攻の SlipDamage 発動
/// 7. 関与者集合（2体）の remaining_actions を -1 し、0 の行を削除（同一トランザクション）
/// </code>
/// ステップ3が2より後にあるのは、行動不能を先に判定するとスキルが確定せず
/// <c>priority</c> が読めなくなり、ステップ2そのものが成立しなくなるため（A-7-i）。
/// </summary>
public sealed class TurnResolver(SkillPlayer skillPlayer)
{
    /// <summary>
    /// 1ターンを解決する。
    ///
    /// 行動が不成立になる条件（対象が Defeated である等）は、呼び出し側が
    /// <see cref="ValidateCommandTarget"/> で事前に弾く。不成立の場合はターン・反撃・
    /// 状態変化の減衰のいずれも発生してはならないため、ターンの内側では扱わない。
    /// </summary>
    /// <param name="counterSkillSelector">
    /// 反撃者が使うスキルを決める。敵の行動パターン（action_pattern_type）と require_tp の
    /// フォールバックがここに載る。
    /// </param>
    /// <param name="onDamageDealt">
    /// 実効ダメージの通知。第1引数は<b>与ダメージの帰属先の entity_id</b>。
    /// ライブ攻撃では行動者、<c>SlipDamage</c> では<b>付与者</b>であり被弾させた側とは限らない
    /// （戦闘システム 6.2）。第2引数は被弾側で、被攻撃TPの蓄積先になる。
    /// </param>
    public TurnResult Resolve(
        BattleParticipant actor,
        Skill actorSkill,
        BattleSession session,
        Random rng,
        Func<BattleParticipant, Skill> counterSkillSelector,
        BattleParticipant? commandTarget = null,
        EnemyAllyTargetSelector? enemyAllySelector = null,
        Action<Guid, BattleParticipant, BigInteger>? onDamageDealt = null)
    {
        var logs = new List<string>();

        // [対象] に敵が指定された場合の CurrentTarget 更新は、そのターンの反撃者が確定する前に行う。
        // 後に置くと、更新した対象は次ターンからしか反撃者にならず指定の意図が失われる
        if (commandTarget is not null && commandTarget.EntityType != actor.EntityType)
            actor.SetTarget(commandTarget.Entity.Id);

        // 反撃者は CurrentTarget の1体のみ。敵の組が複数体でも増えない。
        // 対象解決（と後段へ落ちた場合の書き戻し）はターンにつきここ1回だけ行い、
        // モーションごとには再導出しない。再導出すると、対象を倒した後の残モーションが
        // 別の敵へ乗り換わってしまい「そのモーションのみスキップ」という規則が成立しなくなる
        //
        // ここで関与者集合（A-8）が閉じる。以後、集合の内訳はターン中の出来事で変化しない
        var counter = session.ResolveTarget(actor);
        var counterSkill = counterSkillSelector(counter);

        // ステップ1・2
        var (first, second) = TurnOrder.Decide(
            new TurnSide(actor, actorSkill), new TurnSide(counter, counterSkill), rng);

        // 1対1のターンモデルでは、互いが互いの「相対する相手」になる
        var firstEnemy = second.Participant;
        var secondEnemy = first.Participant;

        // ステップ3・4
        var firstDisabled = RunActionSlot(
            first, firstEnemy, actor, rng, commandTarget, enemyAllySelector, onDamageDealt, logs);

        // ステップ5。先攻の一撃で後攻が戦闘不能・離脱していれば後攻の行動はキャンセルされる。
        // 行動枠そのものが開かないため後攻の SlipDamage も発動しないが、
        // 関与者集合は変わらないため減衰（ステップ7）は通常通り行われる
        var secondCancelled = !second.Participant.IsActive;

        var secondDisabled = secondCancelled
            ? null
            // ステップ5・6
            : RunActionSlot(
                second, secondEnemy, actor, rng, commandTarget, enemyAllySelector, onDamageDealt, logs);

        // ステップ7
        var expired = EffectDecay.Apply(actor, counter);

        return new TurnResult(logs, first, second, secondCancelled, firstDisabled, secondDisabled, expired);
    }

    /// <summary>
    /// 1エンティティぶんの行動枠。行動不能の判定 → モーション再生 → SlipDamage の発動、までを閉じる。
    ///
    /// 行動不能が成立してもこの枠自体は開くため、<c>SlipDamage</c> は発動する（A-7-j）。
    /// 発動しないと、行動不能と毒を併せ持つ相手に対して毒が実質無効化されてしまう。
    /// </summary>
    /// <returns>行動不能を成立させたインスタンス。成立しなければ null。</returns>
    private EffectInstance? RunActionSlot(
        TurnSide side,
        BattleParticipant sideEnemy,
        BattleParticipant actor,
        Random rng,
        BattleParticipant? commandTarget,
        EnemyAllyTargetSelector? enemyAllySelector,
        Action<Guid, BattleParticipant, BigInteger>? onDamageDealt,
        List<string> logs)
    {
        var disabled = DisableMoveJudge.Judge(side.Participant, rng);

        if (disabled is not null)
        {
            // require_tp は消費しない。スキル発動そのものが起きていないため、
            // TPを取ると行動を奪われたうえに資源も失うという二重罰になる（A-7-g）
            logs.Add($"{side.Participant.Entity.Name} は {disabled.Definition.Name} で動けない！");
        }
        else
        {
            // 発動時にTPを消費する。敵の抽選プールは払えるスキルだけで構成され、
            // プレイヤー側はコマンド受理時に弾かれるため、ここで払えないことは通常起こらない。
            // ターン開始からこの時点までにTPが減る経路が無い（被弾は増やす方向にしか働かない）
            side.Participant.TrySpendTp(side.Skill.RequireTp);

            var result = skillPlayer.Play(
                side.Participant, side.Skill, sideEnemy, rng,
                // [対象] は行動したプレイヤーのコマンド引数であり、反撃側には引き継がれない
                commandTarget: side.Participant == actor ? commandTarget : null,
                enemyAllySelector,
                onDamageDealt is null
                    ? null
                    : (attacker, target, damage) => onDamageDealt(attacker.Entity.Id, target, damage));

            logs.AddRange(result.Logs);
        }

        logs.AddRange(SlipDamageRunner.Run(side.Participant, onDamageDealt));

        return disabled;
    }

    /// <summary>
    /// [対象] の受理可否を判定する（戦闘システム 4.2）。
    /// Escaped と Defeated の扱いは非対称であり、将来の蘇生スキルのための布石になっている。
    ///
    /// このゲートは対象解決の直後・ターン開始の直前に置く。ここで拒否された行動は<b>不成立</b>であり、
    /// ターン・TP・反撃・状態変化の減衰のいずれも発生しない。CurrentTarget も書き換えない。
    /// 状態変化の重複付与時の「拒否」（モーションは実行される）とは別物である。
    /// </summary>
    /// <param name="allowsDefeatedTarget">
    /// 実行スキルが蘇生モーションを含むか。現行はその条件が常に偽であるため既定は false。
    /// 可否の切り替え点をこの1箇所に集約している。
    /// </param>
    /// <returns>受理するなら null、拒否するならその理由のメッセージ。</returns>
    public static string? ValidateCommandTarget(
        BattleParticipant? commandTarget, bool allowsDefeatedTarget = false)
    {
        if (commandTarget is null) return null;

        return commandTarget.Status switch
        {
            // 候補集合そのものから除外されるため、本来は解決不能として先に弾かれる
            ParticipantStatus.Escaped => $"{commandTarget.Entity.Name}は戦闘から離脱しています。",

            ParticipantStatus.Defeated when !allowsDefeatedTarget
                => $"{commandTarget.Entity.Name}は既にやられています。",

            _ => null,
        };
    }

    /// <summary>
    /// [対象] の空振りを判定する（戦闘システム 4.2）。
    ///
    /// 「入力が結果に反映されなかったことを必ず通知する」という規則によるもので、
    /// 重複付与時の拒否通知・解除モーションの空振り通知と同型である。
    /// 解決先が行動者自身の場合は、自分自身対象のモーションが実際に作用しているため通知しない
    /// （事実に反する通知を避ける）。
    ///
    /// スキルのモーション列の静的な構成による判定であり、実行時の失敗
    /// （命中失敗・対象状態によるスキップ・重複拒否）とは別レイヤーである。
    /// </summary>
    /// <returns>空振りなら通知メッセージ、そうでなければ null。</returns>
    public static string? DetectAllyTargetMiss(
        BattleParticipant actor, Skill skill, BattleParticipant? commandTarget)
    {
        if (commandTarget is null) return null;

        // 敵の指定はスキル構成を問わず必ず意味を持つ（反撃者の決定という全スキル共通の経路を通るため）。
        // 空振りしうるのは味方の指定のみ
        if (commandTarget.EntityType != actor.EntityType) return null;

        if (commandTarget == actor) return null;
        if (skill.HasAllyTargetMotion) return null;

        return $"{commandTarget.Entity.Name} への影響はありませんでした。";
    }
}

/// <summary>1ターンの解決結果。</summary>
/// <param name="Logs">先攻・後攻の順に整形されたログ列。実行順と一致する。</param>
/// <param name="First">先攻側。</param>
/// <param name="Second">後攻側。</param>
/// <param name="SecondCancelled">先攻の一撃で後攻が Active でなくなり、後攻の行動がキャンセルされたか。</param>
/// <param name="FirstDisabled">
/// 先攻の行動不能を成立させたインスタンス。成立しなければ null。
/// 成立してもターン消費・反撃・減衰は行われるため、ターンの成否とは無関係。
/// </param>
/// <param name="SecondDisabled">後攻の行動不能を成立させたインスタンス。</param>
/// <param name="ExpiredEffects">残り有効行動数を使い切って取り除かれたインスタンス。</param>
public sealed record TurnResult(
    IReadOnlyList<string> Logs,
    TurnSide First,
    TurnSide Second,
    bool SecondCancelled,
    EffectInstance? FirstDisabled = null,
    EffectInstance? SecondDisabled = null,
    IReadOnlyList<(BattleParticipant Holder, EffectInstance Effect)>? ExpiredEffects = null);
