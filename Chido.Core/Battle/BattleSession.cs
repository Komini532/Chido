using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Entities;

namespace Chido.Core.Battle;

public class BattleSession
{
    public Guid   Id        { get; private set; } = Guid.NewGuid();
    public ulong  GuildId   { get; private set; }
    public ulong  ChannelId { get; private set; }

    // 進捗メッセージのIDは持たない。1回の戦闘行動につき1つの新規メッセージを送る方式であり
    // （行動レスポンスと進捗表示を同一メッセージへ集約する）、編集し続ける単一の進捗メッセージが
    // 存在しないため、指し示す対象がそもそも無い。

    public List<BattleLogEntry> Log { get; } = [];
    public DateTimeOffset  LastActionAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt      { get; private set; }
    public BattleEndReason? EndReason   { get; private set; }

    public bool IsFinished => EndedAt.HasValue;

    // --- 参加者管理 ---
    private readonly List<BattleParticipant> _participants = [];
    public IReadOnlyList<BattleParticipant> Participants => _participants;

    public BattleSession(ulong guildId, ulong channelId)
    {
        GuildId   = guildId;
        ChannelId = channelId;
    }

    // 離脱・戦闘不能の発生順の採番。セッション単位で閉じているため、
    // 他のセッションの進行に影響されない
    private ushort _deactivationOrder;

    // 飛び入り参加が前提のため、進行中セッションへの途中参加も常に許可する
    public void AddParticipant(BattleParticipant participant)
    {
        participant.Session = this;
        _participants.Add(participant);
    }

    /// <summary>
    /// 次の消失順の採番。<see cref="BattleParticipant"/> が Active でなくなるときに1度だけ呼ぶ。
    /// </summary>
    internal ushort NextDeactivationOrder() => ++_deactivationOrder;

    /// <summary>
    /// 与ダメージを台帳へ計上する（戦闘システム 6.2）。ターン解決の
    /// <c>onDamageDealt</c> に接続する。
    ///
    /// <b>計上するのは「プレイヤー参加者が敵参加者へ与えた実効ダメージ」だけ。</b>
    /// プレイヤーが味方に与えたダメージ（誤爆・巻き込み）は経験値按分の分子・分母のいずれにも
    /// 含めない。敵の auto 付与による自滅ダメージも、付与者が敵であるため計上されない
    /// （プレイヤーの仕事量ではないため分母に入れてはならない）。
    ///
    /// <c>SlipDamage</c> の与ダメージは<b>付与者</b>へ帰属する。素直に実装すると
    /// 「敵が自分自身に与えたダメージ」になり、毒付与に徹したプレイヤーが報酬ゲートすら
    /// 通過できず対象から完全に除外されてしまう。そのため第1引数は行動者ではなく
    /// 帰属先の entity_id を受ける。
    /// </summary>
    /// <param name="attackerEntityId">与ダメージの帰属先。ライブ攻撃は行動者、スリップは付与者。</param>
    /// <param name="target">被弾側。</param>
    /// <param name="effectiveDamage">実効ダメージ（HPが実際に減った量）。</param>
    public void RecordDamageDealt(
        Guid attackerEntityId, BattleParticipant target, BigInteger effectiveDamage)
    {
        if (target.IsPlayer) return;

        var attacker = _participants.FirstOrDefault(p => p.Entity.Id == attackerEntityId);

        // 付与者が既にセッションから見つからない場合（構造上は起こらないが、
        // 将来セッションを跨ぐ効果が入れば起こりうる）は計上しない
        if (attacker is not { IsPlayer: true }) return;

        attacker.RecordDamageDealt(effectiveDamage);
    }

    // プレイヤーの戦闘行為（攻撃/スキル/戦闘用アイテム）が発生するたびに呼び出す
    public void RecordAction()
    {
        LastActionAt = DateTimeOffset.UtcNow;
    }

    // --- ターゲット解決 ---

    /// <summary>
    /// actor の CurrentTarget を解決する（戦闘システム 3.3）。
    ///
    /// 初回既定・自動失効後の再選定を区別しない<b>単一の導出関数</b>である。
    /// 「初回だから」「対象が死んだから」という契機による分岐を持たず、
    /// 格納値が使えなければ常に後段（Active な display_order 最小の敵）へ落ちる。
    ///
    /// 後段に落ちた場合、その結果を CurrentTargetId へ<b>書き戻す</b>。
    /// 書き戻すことで、明示指定した敵が戦闘不能になった後（将来の蘇生機能で）復活しても
    /// ターゲットが巻き戻らない。
    ///
    /// 「先頭」の順序の唯一の根拠は DisplayOrder であり、参加時刻は使用しない。
    /// 選定結果が常に一意になり、かつプレイヤーから見て「一番上に表示されている敵」＝
    /// 「次にターゲットされる敵」という直感と一致する。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Active な敵が1体も存在しない場合。セッション終了トリガー（敵側の生存が0で
    /// PlayerVictory／EnemyEscaped）により構造的に起こらないため、到達したらフォールバックせず
    /// 実装の不具合として投げる。無言で行動を握り潰すと、セッション終了処理の漏れが検出できなくなる。
    /// </exception>
    public BattleParticipant ResolveTarget(BattleParticipant actor)
    {
        var stored = _participants.FirstOrDefault(p => p.Entity.Id == actor.CurrentTargetId);
        if (stored is { IsActive: true }) return stored;

        var next = _participants
            .Where(p => p.EntityType != actor.EntityType && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .FirstOrDefault();

        if (next is null)
        {
            throw new InvalidOperationException(
                $"{actor.Entity.Name} の対象を解決できない。相対する側に Active な参加者が存在せず、" +
                "本来はセッション終了トリガーが先に発火しているはずである。");
        }

        actor.SetTarget(next.Entity.Id);
        return next;
    }

    /// <summary>
    /// /target 等による明示的なターゲット指定。対象は行動側と異なる EntityType かつ Active である必要がある。
    /// </summary>
    public bool SetTarget(BattleParticipant actor, Guid targetEntityId)
    {
        var target = _participants.FirstOrDefault(p =>
            p.Entity.Id == targetEntityId && p.EntityType != actor.EntityType && p.IsActive);

        if (target is null) return false;

        actor.SetTarget(target.Entity.Id);
        return true;
    }

    // --- 終了判定 ---
    // 「戦闘不能」であること自体は終了トリガーに含めない（戦闘システム 4.3）。
    // 非同期・飛び入り参加前提では参加者の総数がそもそも確定しないため、戦闘不能を含めると
    // 「最初の1人が参加してそのまま戦闘不能になっただけでセッションが終了する」ことになり、
    // 飛び入り参加の前提と矛盾する。全参加者が戦闘不能でもセッションは終了せず、
    // 新規参加者が仕留めに来るか、全員が改めて Escape を選択するまで残り続ける。
    //
    // ここでいう「0人になる」はリストの行数が0になることではなく、該当する側の全参加者の状態が
    // 揃うことを指す（参加者行は物理削除されない）。
    //
    // 3系統は並列に判定する。ChannelMissing だけはこの関数の外から与えられる終了理由であり
    // （戦闘の場そのものが消えたという、参加者の状態には現れない事象のため）、
    // Discord イベントの能動検知とバックグラウンド検証の二層で拾って Finish に直接渡す。
    public (bool ended, BattleEndReason reason) CheckEndCondition()
    {
        var enemies = _participants.Where(p => !p.IsPlayer).ToList();
        var players = _participants.Where(p => p.IsPlayer).ToList();

        // 敵側の生存が0。撃破と逃走の区別は状態分布からは付かないため、最後に消えた1体を見る。
        // 「敵2体のうち1体が逃走しもう1体が撃破された」場合と「1体が逃走した後にプレイヤーが
        // 全員逃走した」場合とでは、テーブル上に同じ状態の組み合わせが現れうる
        if (enemies.Count > 0 && enemies.All(p => !p.IsActive))
        {
            var last = enemies.OrderByDescending(p => p.DeactivationOrder ?? 0).First();

            return (true, last.Status == ParticipantStatus.Escaped
                ? BattleEndReason.EnemyEscaped
                : BattleEndReason.PlayerVictory);
        }

        // 能動的な選択である Escape のみがトリガー。Defeated が混ざっている間は終了しない
        bool allPlayersEscaped = players.Count > 0 && players.All(p => p.Status == ParticipantStatus.Escaped);
        if (allPlayersEscaped) return (true, BattleEndReason.PlayerEscaped);

        return (false, default);
    }

    public void Finish(BattleEndReason reason)
    {
        EndedAt   = DateTimeOffset.UtcNow;
        EndReason = reason;
    }
}
