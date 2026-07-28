using System.Numerics;
using Chido.Core.Battle.Damage;

namespace Chido.Data.Entities;

/// <summary>
/// chido_skill_master: スキルマスタ。
/// 通常攻撃（Attack）と防御（Defend）も本テーブル上の通常のスキルエントリとして表現される。
/// マスタデータはエンティティ種別を問わず共通のため、Attack の行はプレイヤー・敵間で共有される。
/// </summary>
public class SkillMasterRecord
{
    /// <summary>可読キー。</summary>
    public string SkillKey { get; set; } = string.Empty;

    /// <summary>表示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>説明文。</summary>
    public string? Description { get; set; }

    /// <summary>
    /// スキル属性（ビット列）。ダメージ計算には一切使用しない、UI表示専用の"見せかけ"の値。
    /// ダメージ計算が参照するのは chido_skill_motion_attack_master.elements（モーション属性）。
    /// モーション属性からの自動導出は行わず、手動設定を前提とする。
    /// </summary>
    public Element Elements { get; set; }

    /// <summary>
    /// TP消費量（0〜1000）。回復モーションを含むスキルでは 200 以上とする（運用制約）。
    /// 166 以下では被反撃だけでTPが自給でき、回復を毎ターン撃てるため回復威力の実用帯が消滅する。
    /// </summary>
    public ushort RequireTp { get; set; }

    /// <summary>
    /// 習得レベル。NULL=レベルアップでは習得不可（アイテム消費等の他の手段でのみ習得可能）。
    /// exp が DECIMAL(65,0) で level=√exp であるため、レベル最大値の桁数に基づき DECIMAL(33,0) に絞っている。
    /// </summary>
    public BigInteger? LearnableLevel { get; set; }

    /// <summary>
    /// 行動優先度。行動順は priority 降順 → Speed → Random で決まる（戦闘システム 4.1）。
    /// 既定は 0（Attack・通常スキル）。Defend には正の値を与え、Speed に関わらず被弾前に構えを取れるようにする。
    /// </summary>
    public int Priority { get; set; }

    /// <summary>特殊処理呼び出し記号。NULL=標準の効果計算処理のみで完結。</summary>
    public string? SpecialProcessKey { get; set; }
}
