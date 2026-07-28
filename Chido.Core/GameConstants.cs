using Chido.Core.Stats;

namespace Chido.Core;

/// <summary>
/// ゲームルールを構成する定数の集約点。
///
/// 設計ドキュメントは複数箇所で「同一の定数を1箇所で参照する（二重管理を避ける）」ことを
/// 明示的に要求している。ここに集めているのはその要求を満たすためであり、
/// 単に定数をまとめただけの入れ物ではない。
///
/// - Attack / Defend の skill_key … TP+100 の契機判定（4.4）・chido_player_skill の習得管理除外（DB 23番）・
///   priority 既定値の付与（4.1）という3つの参照点が、同じ1箇所を指す必要がある
/// - 草原の field_key … 起動時検証・DrawGroup のフォールバック・NextField のフォールバック・
///   初期フィールド固定の4者が、同じ1箇所を指す必要がある（10.5）
/// - レベルの下限 … exp の初期値（正常系の保証）とレベル取得時のクランプ（異常系のフェイルセーフ）は
///   冗長ではなく二重の防御であり、同一の定数を参照して独立に持たない（2.3）
///
/// バランス調整で頻繁に触る数値（敵のステータス係数・ドロップ率・抽選重み等）はDBマスタ側が持つ。
/// ここにあるのは実質固定値として扱ってよいと判断されたもの、およびゲームルールの構造そのもの。
/// </summary>
public static class GameConstants
{
    // --- スキルキー（戦闘システム 4.1 / 4.4 / 11.2-10） ---

    /// <summary>
    /// 通常攻撃のスキルキー。「対象に威力100%の無属性物理攻撃」という単一モーションを持つ、
    /// スキルマスタ上の通常のエントリであり、特別扱いの別実装ではない。
    /// マスタデータはエンティティ種別を問わず共通のため、この行はプレイヤー・敵間で共有される。
    /// </summary>
    public const string AttackSkillKey = "attack";

    /// <summary>
    /// 防御のスキルキー。実体は target_rule = 自分自身・duration_actions = 1 の DRR 付与モーション1つで、
    /// 反撃モーションを含まない（戦闘システム 4.2）。
    /// </summary>
    public const string DefendSkillKey = "defend";

    // --- TP（戦闘システム 4.4） ---
    // 蓄積量と上限は実質固定値に近く、DBマスタ側で人力調整するメリットが薄いためアプリ側で保持する。

    /// <summary>TPの上限。超過分はカットされる。</summary>
    public const int TpMax = 1000;

    /// <summary>
    /// Attack モーションの再生（＝効果適用への到達）を契機とする蓄積量。
    /// 契機をモーション再生に紐づけることで、敵の「登録された通常攻撃」と
    /// 「フォールバックの通常攻撃」の両経路が同一の1点に合流する。
    /// </summary>
    public const int TpGainOnAttackMotion = 100;

    /// <summary>Defend モーション（DRR 付与モーション）の再生を契機とする蓄積量。</summary>
    public const int TpGainOnDefendMotion = 100;

    /// <summary>
    /// 被攻撃時の蓄積量の分子。floor(TpGainOnDamagedNumerator × 被ダメージ ÷ 最大HP) を加算する。
    /// 被ダメージは実効ダメージ（台帳計上値）を指し、ライブ攻撃・SlipDamage の双方で同一の定義を用いる。
    /// </summary>
    public const int TpGainOnDamagedNumerator = 500;

    // --- ステータス算出（戦闘システム 2.3） ---
    // 基礎ステータス = レベル × Scale(ステータス区分) × Shape(ステータス区分)

    /// <summary>HP の Scale。</summary>
    public const int LifeScale = 12;

    /// <summary>物理／魔法攻撃力の Scale。</summary>
    public const int AttackScale = 8;

    /// <summary>
    /// 物理／魔法防御力の Scale。AttackScale と同値であることが、同格同士の被防御係数を
    /// ちょうど 0.5 にし、「等価な回復威力 = 攻撃威力 × 被防御係数」の較正の起点になっている（5.1）。
    /// </summary>
    public const int DefenseScale = 8;

    /// <summary>プレイヤーの Shape。ポケモンの種族値に相当する値で、プレイヤーはすべて 1.00 固定。</summary>
    /// <remarks>_shape 列は permyriad ではなく 100 = 1.00 のスケールで格納される（DB設計の命名規約）。</remarks>
    public const int PlayerShape = 100;

    /// <summary>
    /// プレイヤーの基本 Speed。Scale × Shape の枠組みには含まれない固定値であり、
    /// 変動要因は装備効果のみ（強さ倍率・状態変化補正の影響を受けない）。
    /// </summary>
    public const int PlayerBaseSpeed = 500;

    /// <summary>プレイヤー・敵に共通の Luck の基本値（0%）。変動要因は装備効果のみ。</summary>
    public static readonly Ratio BaseLuck = Ratio.Zero;

    // --- 初期値・下限（戦闘システム 2.3 / 10.5） ---

    /// <summary>
    /// レベルの下限であり、新規プレイヤーの経験値の初期値でもある。
    /// level = max(1, floor(√exp)) において exp = 0 はレベル0＝全ステータス0を意味し、
    /// プレイヤーが成立しない。初期値（正常系）とクランプ（異常系）は同一のこの定数を参照する。
    /// </summary>
    public const int MinLevel = 1;

    /// <summary>新規プレイヤーの経験値の初期値。<see cref="MinLevel"/> と同一の値を指す（二重管理を避ける）。</summary>
    public const int InitialExp = MinLevel;

    /// <summary>
    /// 戦闘チャンネル初期化時の累積敵レベル。敵レベルは累積敵レベルから直接与えられ、
    /// 初期値1・減少なしであるため常に1以上が保証される
    /// （プレイヤー側のクランプに相当する保証を、敵側は初期値と単調増加で担保する）。
    /// </summary>
    public const int InitialCumulativeEnemyLevel = MinLevel;

    // --- フィールド（戦闘システム 10.4 / 10.5） ---

    /// <summary>
    /// 最初のフィールド、および各種フォールバック先となる「草原」のフィールドキー。
    /// DBのデフォルト値には委ねず、アプリ側の定数として解決する。
    /// 組の抽選・フィールド遷移のいずれが破綻しても草原へ落ちる、という単一の縮退規則の基点。
    /// </summary>
    public const string GrasslandFieldKey = "grassland";

    /// <summary>
    /// フィールド切替の周期。累積敵レベルがこの倍数に達するたびに切り替わる。
    /// 切替専用の別カウンターは存在せず、累積敵レベルそのものを見る。
    /// </summary>
    public const int FieldTransitionPeriod = 2500;

    // --- ダメージ計算（戦闘システム 5.1 / 5.2 / 5.4） ---

    /// <summary>クリティカル発生率。ポケモンの通常クリティカル率に近い水準。</summary>
    public static readonly Ratio CriticalRate = Ratio.FromPercent(4m);

    /// <summary>クリティカル倍率。PostDefense フェーズで最終ダメージに乗算する。回復量には適用しない。</summary>
    public static readonly Ratio CriticalMultiplier = Ratio.FromMultiplier(1.5m);

    /// <summary>
    /// 防御（Defend）が付与するダメージ軽減率（DRR）。固定値のため
    /// chido_effect_status_modifier_master.fixed_rate = 5000 の固定値行として持てる。
    /// 軽減の対象は「CurrentTarget からの被ダメージ」ではなく、そのターンに自分が受ける全ダメージ
    /// （ただし SlipDamage には適用しない）。
    /// </summary>
    public static readonly Ratio DefendDamageResistRate = Ratio.Half;

    /// <summary>
    /// 攻撃・スリップパイプラインが保証する最低ダメージ。
    /// 命中判定を外したモーションはパイプラインに入らないため、この保証にも到達しない。
    /// 回復パイプラインの下限は0であり、この値ではない。
    /// </summary>
    public const int MinimumDamage = 1;

    /// <summary>
    /// 属性補正 1.3^x を有理数で表すための分子。倍率は x ≥ 0 で × 13^x ÷ 10^x、
    /// x &lt; 0 で × 10^|x| ÷ 13^|x| として適用し、浮動小数点を通さない（5.1 の丸め規則）。
    /// </summary>
    public const int ElementAffinityNumerator = 13;

    /// <summary>属性補正 1.3^x を有理数で表すための分母。</summary>
    public const int ElementAffinityDenominator = 10;

    // --- 装備（戦闘システム 2.3・2.5、DB設計25番） ---

    /// <summary>
    /// 装備のレアリティ補正 1.2^rarity を有理数で表すための分子。
    /// 1スロットの補正値 = progression_value × 1.2^rarity × *_rate であり、
    /// 属性補正と同じく浮動小数点を通さずに累乗するため分数で保持する。
    /// </summary>
    public const int RarityMultiplierNumerator = 6;

    /// <summary>装備のレアリティ補正 1.2^rarity を有理数で表すための分母。</summary>
    public const int RarityMultiplierDenominator = 5;
}
