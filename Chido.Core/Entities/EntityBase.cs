using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Stats;

namespace Chido.Core.Entities;

/// <summary>
/// プレイヤー・敵に共通するステータス算出と現在HPの管理。
///
/// <b>ステータスは保持せず、参照のたびにレベル・装備・状態変化から算出する</b>（戦闘システム 2.5）。
/// 値をフィールドに持って明示的な再計算に頼ると、装備変更・状態変化の付与解除のたびに
/// 再計算を呼び忘れる余地が生まれる。動的算出であれば、装備変更が即座に戦闘へ反映されるという
/// 決定事項もそのまま満たせる。
///
/// 種族ごとにC#クラスを増やすとクラス数が際限なく膨らむため、敵は単一の
/// <see cref="Enemies.Enemy"/> にデータを流し込む。データ駆動＋継承のハイブリッド（戦闘システム 3.1）。
/// </summary>
public abstract class EntityBase : IEntity
{
    // 装着中の装備。5スロットぶんが加算合成されて1つの乗算項になる（レイヤー内は加算）
    private readonly List<EquipmentBonus> _equipment = [];

    // 併存する状態変化インスタンスのステータス変動成分。同じく加算合成される
    private readonly List<StatusModifier> _statusModifiers = [];

    // 一時付与属性。本体属性・装備属性と OR される。保持数の上限は設けない（戦闘システム 5.3）
    private Element _grantedElements = Element.None;

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public abstract string Name { get; }

    public abstract BigInteger Level { get; }

    /// <summary>ステータス区分ごとの Shape。プレイヤーは <see cref="StatShape.Player"/>（等倍）。</summary>
    protected abstract StatShape Shape { get; }

    /// <summary>強さ倍率。プレイヤーは常に等倍で、敵のみ個別設定を持つ。</summary>
    protected abstract Ratio StrengthRate { get; }

    /// <summary>Speed の基本値。プレイヤーは 500 固定、敵は個別値。</summary>
    protected abstract int BaseSpeed { get; }

    /// <summary>
    /// 本体属性。プレイヤーは常に <see cref="Element.None"/>（属性は装備由来のみ）で、
    /// 敵のみ個別設定を持つ（戦闘システム 2.4）。
    /// </summary>
    protected abstract Element InnateElements { get; }

    // --- 戦闘時ステータス ---

    public BigInteger MaxLife => CombatStat(
        GameConstants.LifeScale, Shape.MaxLife, EquipmentSum(e => e.MaxLifeRate), TargetStatus.MaxLife);

    public BigInteger PAtk => CombatStat(
        GameConstants.AttackScale, Shape.PAtk, EquipmentSum(e => e.PAtkRate), TargetStatus.PAtk);

    public BigInteger PDef => CombatStat(
        GameConstants.DefenseScale, Shape.PDef, EquipmentSum(e => e.PDefRate), TargetStatus.PDef);

    public BigInteger MAtk => CombatStat(
        GameConstants.AttackScale, Shape.MAtk, EquipmentSum(e => e.MAtkRate), TargetStatus.MAtk);

    public BigInteger MDef => CombatStat(
        GameConstants.DefenseScale, Shape.MDef, EquipmentSum(e => e.MDefRate), TargetStatus.MDef);

    // Speed と Luck は progression_value × 1.2^rarity のスケーリングを受けず、装備の生の値を加算する
    public int Speed => StatCalculator.Speed(BaseSpeed, _equipment.Sum(e => e.SpeedBonus));

    public Ratio Luck => StatCalculator.Luck(
        GameConstants.BaseLuck,
        _equipment.Aggregate(Ratio.Zero, (acc, e) => acc + e.LuckBonusRate));

    public Element Elements =>
        InnateElements | _equipment.Aggregate(Element.None, (acc, e) => acc | e.Elements) | _grantedElements;

    public Ratio DamageResistRate => ToRatio(StatusSum(TargetStatus.DamageResistRate));

    // --- 現在HP ---

    public BigInteger CurrentLife { get; protected set; }

    public bool IsAlive => CurrentLife > BigInteger.Zero;

    /// <summary>
    /// セッション参加時・敵の出現時に現在HPを全快させる。戦闘ごとに全快するのは意図した仕様であり、
    /// その帰結として戦闘外の回復手段は設計上存在しない（戦闘システム 3.4）。
    /// オーバーヒール状態はここで書き込まれる MaxLife により自然に解消される。
    /// </summary>
    public void RestoreToFull() => CurrentLife = MaxLife;

    /// <summary>
    /// ダメージを適用し、実効ダメージ（＝ min(damage, 適用直前の現在HP)）を返す。
    ///
    /// 最低1の保証はここでは行わない。それはダメージパイプライン側の責務であり（戦闘システム 5.1）、
    /// 命中判定を外したモーションはそもそもパイプラインに入らないためこの関数にも到達しない。
    /// ここで下限を敷くと「実効0であるべき経路」（とどめ以降のスリップ等）を表現できなくなる。
    /// </summary>
    public virtual BigInteger TakeDamage(BigInteger damage)
    {
        if (damage <= BigInteger.Zero) return BigInteger.Zero;

        var before = CurrentLife;
        CurrentLife = BigInteger.Max(BigInteger.Zero, CurrentLife - damage);
        return before - CurrentLife;
    }

    /// <summary>
    /// 回復を適用し、実際に増加した量を返す。
    ///
    /// <b>MaxLife でのクランプは行わない</b>（戦闘システム 3.4）。MaxLife は装備・状態変化から
    /// 毎回動的に算出される値であり、切り詰めると「装備を一時的に外して戻すとHPが減る」という
    /// 不可逆な副作用が生まれる。クランプしないことで MaxLife の変動を現在HPから完全に独立させる。
    /// </summary>
    public virtual BigInteger Heal(BigInteger amount)
    {
        if (amount <= BigInteger.Zero) return BigInteger.Zero;

        CurrentLife += amount;
        return amount;
    }

    /// <summary>
    /// 現在HP割合（表示用）。
    /// オーバーヒール時は 100% を超えうるため permyriad が int の範囲を超えることがあり、
    /// その場合は表示上の意味が失われない範囲で上限に張り付かせる。
    ///
    /// <b>敵の ally_target_rule = 24（HP割合最小）の比較には使わないこと。</b>
    /// あちらは除算せず交差乗算で比較すると定められており（戦闘システム 4.2）、
    /// 本プロパティは丸めを伴うため同順位の判定がずれる。
    /// </summary>
    public Ratio LifeRatio
    {
        get
        {
            if (MaxLife <= BigInteger.Zero) return Ratio.Zero;

            var permyriad = CurrentLife * Ratio.Full.Permyriad / MaxLife;
            if (permyriad > int.MaxValue) return Ratio.FromPermyriad(int.MaxValue);
            if (permyriad < int.MinValue) return Ratio.FromPermyriad(int.MinValue);
            return Ratio.FromPermyriad((int)permyriad);
        }
    }

    // --- 装備レイヤー ---

    public IReadOnlyList<EquipmentBonus> Equipment => _equipment;

    /// <summary>
    /// 装着中の装備を差し替える。戦闘中の装備変更は許容されており、ステータスが動的算出であるため
    /// 変更は即座に反映される。最大HPが減少しても現在HPのクランプは行わない。
    /// </summary>
    public void SetEquipment(IEnumerable<EquipmentBonus> equipment)
    {
        _equipment.Clear();
        _equipment.AddRange(equipment);
    }

    // --- 状態変化レイヤー ---
    // 付与・解除のライフサイクル（重複判定・残り有効行動数の減衰）は状態変化の実装時に
    // このリストを駆動する形で載る。ここが持つのは合成に必要な変動量のみ。

    public IReadOnlyList<StatusModifier> StatusModifiers => _statusModifiers;

    public void AddStatusModifier(StatusModifier modifier) => _statusModifiers.Add(modifier);

    public bool RemoveStatusModifier(StatusModifier modifier) => _statusModifiers.Remove(modifier);

    public void ClearStatusModifiers() => _statusModifiers.Clear();

    // --- 一時付与属性 ---

    public void GrantElements(Element elements) => _grantedElements |= elements;

    public void ClearGrantedElements() => _grantedElements = Element.None;

    // --- 内部 ---

    private BigInteger CombatStat(int scale, int shape, BigInteger equipmentSum, TargetStatus targetStatus)
        => StatCalculator.CombatStat(Level, scale, shape, StrengthRate, equipmentSum, StatusSum(targetStatus));

    /// <summary>
    /// 装備レイヤーの Σ（permyriad）。各スロットの補正値は
    /// progression_value × 1.2^rarity × rate で算出されるため、合計は int に収まらず BigInteger になる。
    /// </summary>
    private BigInteger EquipmentSum(Func<EquipmentBonus, Ratio> selector)
        => _equipment.Aggregate(BigInteger.Zero, (acc, e) => acc + e.ContributionOf(selector(e)));

    /// <summary>状態変化レイヤーの Σ（permyriad）。装備側と型を揃えるため BigInteger で合算する。</summary>
    private BigInteger StatusSum(TargetStatus targetStatus)
        => _statusModifiers
            .Where(m => m.TargetStatus == targetStatus)
            .Aggregate(BigInteger.Zero, (acc, m) => acc + m.Rate.Permyriad);

    /// <summary>
    /// permyriad の合計を Ratio へ戻す。Ratio の内部表現は int のため、
    /// 現実には起こらないが範囲外になった場合は飽和させる（例外で戦闘を止めない）。
    /// </summary>
    private static Ratio ToRatio(BigInteger permyriad)
    {
        if (permyriad > int.MaxValue) return Ratio.FromPermyriad(int.MaxValue);
        if (permyriad < int.MinValue) return Ratio.FromPermyriad(int.MinValue);
        return Ratio.FromPermyriad((int)permyriad);
    }
}
