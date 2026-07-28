using Chido.Core.Battle.Damage;
using Chido.Core.Stats;

namespace Chido.Core.Battle.Skills;

/// <summary>
/// スキルモーション（chido_skill_motion_master とそのサブタイプ 10a〜10d に対応）。
/// スキルは1つ以上のモーションから構成され、<see cref="MotionIndex"/> 昇順に再生される。
///
/// 全モーションが共通して持つのは <see cref="TargetRule"/> / <see cref="AccuracyRate"/> /
/// <see cref="AccuracyGateGroup"/> の3つだけであり、それ以外はサブタイプ固有のパラメータになる。
/// 攻撃と回復は列構成も意味論も異なる（被防御係数・クリティカル・属性相性が攻撃にのみ適用される）ため、
/// motion_type を廃して power を符号付きにするような統合は行わない。
/// </summary>
public abstract record SkillMotion(
    byte MotionIndex,
    TargetRule TargetRule,
    Ratio AccuracyRate,
    ushort? AccuracyGateGroup = null)
{
    /// <summary>サブタイプの判別子。DB上は複合FKの構成列でもある。</summary>
    public abstract MotionType MotionType { get; }
}

/// <summary>攻撃モーション（10a）。属性を持つ唯一のモーション種別。</summary>
public sealed record AttackMotion(
    byte MotionIndex,
    TargetRule TargetRule,
    Ratio AccuracyRate,
    AttackType AttackType,
    int Power,
    Element Elements = Element.None,
    ushort? AccuracyGateGroup = null)
    : SkillMotion(MotionIndex, TargetRule, AccuracyRate, AccuracyGateGroup)
{
    public override MotionType MotionType => MotionType.Attack;
}

/// <summary>
/// 回復モーション（10b）。属性を持たず、対象の防御力もクリティカルもDRRも参照しない。
/// </summary>
public sealed record HealMotion(
    byte MotionIndex,
    TargetRule TargetRule,
    Ratio AccuracyRate,
    AttackType AttackType,
    int Power,
    ushort? AccuracyGateGroup = null)
    : SkillMotion(MotionIndex, TargetRule, AccuracyRate, AccuracyGateGroup)
{
    public override MotionType MotionType => MotionType.Heal;
}

/// <summary>
/// 状態変化付与モーション（10c）。
/// <see cref="AttackType"/> は付与する状態変化が SlipDamage 成分を持つ場合に、
/// 継続ダメージが物理／魔法どちらの攻撃力を基準にするかを決める（付与時にインスタンスへ複製される）。
/// </summary>
public sealed record GrantEffectMotion(
    byte MotionIndex,
    TargetRule TargetRule,
    Ratio AccuracyRate,
    string EffectKey,
    Ratio? EffectRate = null,
    AttackType? AttackType = null,
    ushort? DurationActions = null,
    ushort? AccuracyGateGroup = null)
    : SkillMotion(MotionIndex, TargetRule, AccuracyRate, AccuracyGateGroup)
{
    public override MotionType MotionType => MotionType.GrantEffect;
}

/// <summary>
/// 戦闘離脱モーション（サブタイプなし。可変パラメータを持たないため）。
///
/// /escape コマンドと同一の戦闘離脱処理をモーション経由で呼び出すが、
/// <see cref="SkillMotion.AccuracyRate"/> を持つため<b>失敗しうる</b>点が異なる
/// （/escape はモーションを経由せず必ず成功する）。
/// 離脱するのは target_rule により解決された対象であり、target_rule = 敵 の離脱モーションは
/// プレイヤーが敵を「追い払う」手段として成立する。
/// </summary>
public sealed record FleeMotion(
    byte MotionIndex,
    TargetRule TargetRule,
    Ratio AccuracyRate,
    ushort? AccuracyGateGroup = null)
    : SkillMotion(MotionIndex, TargetRule, AccuracyRate, AccuracyGateGroup)
{
    public override MotionType MotionType => MotionType.Flee;
}

/// <summary>
/// 状態変化解除モーション（10d）。
/// 対象が保持する全スコープから effect_key が一致する行をすべて削除する。
/// 付与者・付与元・付与要因は参照しない（解毒は毒の出所を問わないため、付与の重複判定とは意図的に非対称）。
/// </summary>
public sealed record DispelEffectMotion(
    byte MotionIndex,
    TargetRule TargetRule,
    Ratio AccuracyRate,
    string EffectKey,
    ushort? AccuracyGateGroup = null)
    : SkillMotion(MotionIndex, TargetRule, AccuracyRate, AccuracyGateGroup)
{
    public override MotionType MotionType => MotionType.DispelEffect;
}
