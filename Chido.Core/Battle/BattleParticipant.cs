using System;
using System.Numerics;
using Chido.Core.Entities;

namespace Chido.Core.Battle;

public class BattleParticipant
{
    public IEntity        Entity        { get; }
    public EntityType     EntityType    { get; }
    public ulong?         DiscordUserId { get; } // プレイヤーのみ設定 (DB: user_id)
    public Guid?          EnemyId       { get; } // 敵のみ設定。敵出現インスタンスの使い捨てGuid (DB: enemy_id)

    /// <summary>
    /// 表示順。entity_type ごとに独立した番号空間を持つ。
    /// Enemy : 組の member_index の恒等複製。ターゲット自動再選定における「先頭の敵」の唯一の根拠。
    /// Player: セッション内の参加順。Discord埋め込みの表示順にのみ使用される。
    ///
    /// 時刻列（JoinedAt）を順序キーに流用しないための専用の列。DATETIME(3) は
    /// 「同時に走らない」ことが保証されていても「別ミリ秒になること」は保証しないため、
    /// 一括INSERTされる敵の組では順序が一意に定まらない。
    /// </summary>
    public ushort DisplayOrder { get; }

    /// <summary>参加時刻の記録。順序付けには使用しない（DisplayOrder がその責務を持つ）。</summary>
    public DateTimeOffset JoinedAt { get; }

    public bool IsPlayer => EntityType == EntityType.Player;

    // HP=0 からの間接判定ではなく、状態そのものを一次情報として保持する
    public ParticipantStatus Status { get; private set; } = ParticipantStatus.Active;
    public bool IsActive => Status == ParticipantStatus.Active;

    // 現在相対している敵 (Attack/Skill/Defend の対象解決に使う)。/target や行動コマンドの都度指定で更新される
    public Guid? CurrentTargetId { get; private set; }

    /// <summary>
    /// 所属セッション。<see cref="BattleSession.AddParticipant"/> が設定する。
    /// 離脱・戦闘不能の発生順（<see cref="DeactivationOrder"/>）をセッション単位で採番するためだけに持つ。
    /// </summary>
    internal BattleSession? Session { get; set; }

    /// <summary>
    /// Active でなくなった順番。まだ Active なら null。
    ///
    /// <b>「最後の1体の消失原因」を判定するためだけに存在する。</b>
    /// 敵側の生存が0になったとき、それが <see cref="BattleEndReason.PlayerVictory"/> なのか
    /// <see cref="BattleEndReason.EnemyEscaped"/> なのかは、参加者の状態分布からは事後的に区別できない
    /// （Escaped の敵行と Defeated の敵行は同居しうる）。終了理由は次に出現する敵の抽選を分岐させるため、
    /// どちらであったかを取り違えられない（戦闘システム 4.3・10.3）。
    ///
    /// 一方向の遷移であり、最初の遷移でのみ確定する。
    /// </summary>
    public ushort? DeactivationOrder { get; private set; }

    // --- TP（戦闘システム 4.4） ---

    /// <summary>
    /// 現在のTP（0〜<see cref="GameConstants.TpMax"/>）。上限超過分はカットされる。
    /// プレイヤーはセッション参加時に0、敵は出現時に enemy_master.initial_tp で初期化される
    /// （この非対称は意図的な据え置き。プレイヤー側にも初期TPを持たせる拡張余地を残している）。
    /// </summary>
    public ushort CurrentTp { get; private set; }

    /// <summary>
    /// ローテーションの現在位置（敵のみ意味を持つ。出現時に0で初期化される）。
    ///
    /// <b>真実の情報源はこの値であり、<c>(turn - 1) % total</c> ではない。</b>
    /// ターン数から導出すると、複数の敵が同一ターンに行動する構成で全敵のローテ位置が
    /// 同じターン数を参照して同期してしまう。「ローテ末尾で自己バフをリフレッシュし先頭に戻る／
    /// プレイヤーがそれを妨害して不発にさせる」というデザインは、位置が同期すると成立しない。
    /// 1対1では結果的に一致するが、それは観測される従属式にすぎない（戦闘システム 4.2）。
    /// </summary>
    public ushort RotationIndex { get; private set; }

    public BattleParticipant(
        IEntity         entity,
        EntityType      entityType,
        ulong?          discordUserId = null,
        Guid?           enemyId       = null,
        ushort          displayOrder  = 0,
        DateTimeOffset? joinedAt      = null,
        ushort          initialTp     = 0)
    {
        Entity        = entity;
        EntityType    = entityType;
        DiscordUserId = discordUserId;
        EnemyId       = enemyId;
        DisplayOrder  = displayOrder;
        JoinedAt      = joinedAt ?? DateTimeOffset.UtcNow;
        CurrentTp     = Clamp(initialTp);
    }

    public void SetTarget(Guid? targetEntityId) => CurrentTargetId = targetEntityId;

    public void MarkDefeated() => Deactivate(ParticipantStatus.Defeated);

    public void MarkEscaped() => Deactivate(ParticipantStatus.Escaped);

    /// <summary>
    /// TPを蓄積する。上限超過分はカットされ、繰り越されない。
    /// </summary>
    public void GainTp(int amount)
    {
        if (amount <= 0) return;

        CurrentTp = Clamp(CurrentTp + amount);
    }

    /// <summary>
    /// 被弾によるTP蓄積（戦闘システム 4.4）。<c>floor(500 × 実効ダメージ ÷ 最大HP)</c>。
    ///
    /// 蓄積の主体は<b>被弾側</b>であり、ライブ攻撃・<c>SlipDamage</c> のいずれで受けたダメージでも
    /// 同一の定義（実効ダメージ＝台帳計上値）を用いる。<c>SlipDamage</c> はインスタンス単位で加算するため、
    /// 併存する微小なスリップが各回0に落ちて蓄積されないことは実用上の許容誤差として意図的である。
    ///
    /// auto 付与の自滅スリップでは与ダメージ計上と被弾が同一エンティティになるため、
    /// 自分の自滅ダメージで自分のTPが蓄積される。これも意図した挙動。
    /// </summary>
    public void GainTpOnDamaged(BigInteger effectiveDamage)
    {
        if (effectiveDamage <= BigInteger.Zero) return;

        var maxLife = Entity.MaxLife;
        if (maxLife <= BigInteger.Zero) return;

        var gain = GameConstants.TpGainOnDamagedNumerator * effectiveDamage / maxLife;

        // 実効ダメージは適用直前HPで頭打ちになるため 500 を超えることは無いが、
        // オーバーヒール状態からの被弾など比が想定外に振れた場合も上限で吸収する
        GainTp(gain > GameConstants.TpMax ? GameConstants.TpMax : (int)gain);
    }

    /// <summary>
    /// スキル発動のためTPを消費する。足りなければ消費せず false を返す。
    ///
    /// 行動不能（DisableMove）が成立した場合は<b>呼ばない</b>。スキル発動そのものが
    /// 起きていないため、TPを取ると二重罰になる（戦闘システム 5.4 / A-7-g）。
    /// </summary>
    public bool TrySpendTp(ushort requireTp)
    {
        if (CurrentTp < requireTp) return false;

        CurrentTp -= requireTp;
        return true;
    }

    /// <summary>そのスキルを発動できるだけのTPを持つか。敵の抽選プールの構成条件でもある。</summary>
    public bool CanAfford(ushort requireTp) => CurrentTp >= requireTp;

    /// <summary>
    /// ローテーションを1つ進める（戦闘システム 4.2）。
    ///
    /// <b>敵にターンが回るたびに、選択スキルの成否・require_tp フォールバックの有無・
    /// 行動不能の成立に関わらず前進する。</b>
    /// 前進させないとローテが凍結し、行動不能が解けた後に同じスキルへ戻ってしまう（A-7-h）。
    /// </summary>
    /// <param name="total">登録スキル数。戦闘中は不変。</param>
    public void AdvanceRotation(int total)
    {
        if (total <= 0) return;

        RotationIndex = (ushort)((RotationIndex + 1) % total);
    }

    /// <summary>
    /// Active から降りる。<c>Defeated → Escaped</c> のみ再遷移を許す。
    ///
    /// <c>/escape</c> は Active・Defeated のいずれの状態からも実行できる（生死を問わない）。
    /// これが戦闘不能プレイヤーの唯一の能動的な脱出手段であり、単一セッション制約による拘束は
    /// この経路でしか自力では解けない（報酬を放棄することになる。戦闘システム 4.3）。
    /// 逆に <see cref="ParticipantStatus.Escaped"/> は終端であり、同じ戦闘には再参加できない。
    ///
    /// 消失順は<b>最初に Active でなくなった時点</b>で確定し、以後の再遷移では動かさない。
    /// 「敵側の生存が0になった順番」を表す値であり、既に生存から外れた参加者の位置は変わらないため。
    /// </summary>
    private void Deactivate(ParticipantStatus status)
    {
        if (Status == ParticipantStatus.Escaped) return;
        if (Status == status) return;

        Status = status;
        DeactivationOrder ??= Session?.NextDeactivationOrder() ?? 0;
    }

    private static ushort Clamp(int value)
        => value < 0 ? (ushort)0
            : value > GameConstants.TpMax ? (ushort)GameConstants.TpMax
            : (ushort)value;
}
