using System.Numerics;
using Chido.Core;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Equipment;
using Chido.Core.Items;
using Chido.Core.Progression;
using Chido.Core.Stats;
using Chido.Data.Entities;

namespace Chido.Data.Seeding;

/// <summary>
/// 初期マスタデータの定義（戦闘システム 10.5）。
///
/// <para>
/// <b>マイグレーションの <c>HasData</c> には載せない。</b>ここにあるのはスキーマではなく
/// ゲームバランスであり、調整のたびにマイグレーションが1本増える運用は釣り合わない。
/// 投入は <see cref="MasterDataSeeder"/> が行い、<b>既にあるキーは書き換えない</b>ため、
/// 運用側が手で調整した値を後から踏み潰すこともない。
/// </para>
/// <para>
/// ここに並ぶのは「これが無いとゲームが成立しない最小限」と、
/// 一通りの経路（属性・状態変化・装備・アイテム・称号）が動くことを確かめられるだけの見本。
/// 本格的なコンテンツの追加は運用側でこの表に足していく想定。
/// </para>
/// </summary>
public static class MasterData
{
    // --- キー ---
    // 可読キーはアプリ側から参照されるものだけを定数にする。
    // 通常攻撃・防御・草原は GameConstants が持つ（参照点が複数あるため）。

    public const string PoisonEffectKey = "poison";
    public const string AttackUpEffectKey = "attack_up";
    public const string ParalysisEffectKey = "paralysis";

    public const string SlimeKey = "slime";
    public const string BatKey = "bat";
    public const string WolfKey = "wolf";

    public const string PotionItemKey = "potion";
    public const string AntidoteItemKey = "antidote";

    /// <summary>
    /// 敵の基準となる Shape。プレイヤーの <see cref="GameConstants.PlayerShape"/>（100 = 1.00）と
    /// 同じスケール。同格の敵と真正面から殴り合うと互いに3発で沈む較正（5.1）が起点。
    /// </summary>
    private const ushort BaseShape = GameConstants.PlayerShape;

    // --- フィールド ---

    /// <summary>
    /// フィールド。草原は <see cref="GameConstants.GrasslandFieldKey"/> であり、
    /// 起動時検証・組の抽選の縮退先・遷移の縮退先・初期フィールドの4者が同じキーを見る。
    /// </summary>
    public static IReadOnlyList<FieldMasterRecord> Fields =>
    [
        new() { FieldKey = GameConstants.GrasslandFieldKey, Name = "草原" },
        new() { FieldKey = "cave", Name = "洞窟" },
    ];

    /// <summary>
    /// フィールド別のレアリティ抽選率。
    ///
    /// <b><see cref="Rarity.Hidden"/> の行は作らない。</b>イベント専用であり通常の抽選に現れてはならない
    /// （<c>GroupDraw</c> 側でも除外しているが、そもそも候補として置かない）。
    /// 合計が 10000 に満たなくても実際の合計で正規化されるため、余りが縮退経路へ落ちることはない。
    /// </summary>
    public static IReadOnlyList<FieldRarityRateMasterRecord> RarityRates =>
    [
        Rate(GameConstants.GrasslandFieldKey, Rarity.Common, 70m),
        Rate(GameConstants.GrasslandFieldKey, Rarity.Uncommon, 25m),
        Rate(GameConstants.GrasslandFieldKey, Rarity.Rare, 5m),

        Rate("cave", Rarity.Common, 55m),
        Rate("cave", Rarity.Uncommon, 35m),
        Rate("cave", Rarity.Rare, 10m),
    ];

    /// <summary>
    /// 遷移先。<b>草原の自己ループを必ず含める。</b>
    /// 自己ループは「意図した行き止まり」であり、遷移先0件による縮退とは区別される
    /// （0件だと縮退の通知が出てしまい、正常な進行がマスタ不整合として報告される）。
    /// </summary>
    public static IReadOnlyList<FieldTransitionMasterRecord> Transitions =>
    [
        Transition(GameConstants.GrasslandFieldKey, GameConstants.GrasslandFieldKey),
        Transition(GameConstants.GrasslandFieldKey, "cave"),
        Transition("cave", GameConstants.GrasslandFieldKey),
        Transition("cave", "cave"),
    ];

    // --- 敵の組 ---

    public static IReadOnlyList<EnemyGroupMasterRecord> Groups =>
    [
        new() { GroupKey = "slime_solo", Rarity = Rarity.Common },
        new() { GroupKey = "bat_pair", Rarity = Rarity.Uncommon },
        new() { GroupKey = "wolf_pack", Rarity = Rarity.Rare },
    ];

    public static IReadOnlyList<EnemyGroupMemberMasterRecord> GroupMembers =>
    [
        Member("slime_solo", 0, SlimeKey),

        // 同一種族が複数体。表示名の衝突（オートコンプリートの #n 付与）もここで自然に起きる
        Member("bat_pair", 0, BatKey),
        Member("bat_pair", 1, BatKey),

        Member("wolf_pack", 0, WolfKey),
        Member("wolf_pack", 1, SlimeKey),
    ];

    /// <summary>
    /// フィールドと組の紐づけ。<b>草原の <see cref="Rarity.Common"/> は起動時検証の対象</b>であり、
    /// これが欠けると Bot が起動しない。
    /// </summary>
    public static IReadOnlyList<FieldEnemyGroupMasterRecord> FieldGroups =>
    [
        FieldGroup(GameConstants.GrasslandFieldKey, Rarity.Common, "slime_solo"),
        FieldGroup(GameConstants.GrasslandFieldKey, Rarity.Uncommon, "bat_pair"),
        FieldGroup(GameConstants.GrasslandFieldKey, Rarity.Rare, "wolf_pack"),

        FieldGroup("cave", Rarity.Common, "slime_solo"),
        FieldGroup("cave", Rarity.Uncommon, "bat_pair"),
        FieldGroup("cave", Rarity.Rare, "wolf_pack"),
    ];

    // --- 敵 ---

    /// <summary>
    /// 敵。<c>Shape</c> は 100 = 1.00 のスケールで、合計がプレイヤー（全ステータス100）と
    /// 釣り合う配分を基準にする。強さの差は Shape の配分と強さ倍率で表現し、
    /// レベルは組の全メンバーで共通であるため個体側では持たない。
    /// </summary>
    public static IReadOnlyList<EnemyMasterRecord> Enemies =>
    [
        // 最初に出会う敵。同格よりやや弱く、初見で負けない水準
        new()
        {
            EnemyKey = SlimeKey,
            Name = "スライム",
            Rarity = Rarity.Common,
            Elements = Element.Water,
            HpShape = 120, PAtkShape = 80, PDefShape = 90, MAtkShape = 60, MDefShape = 90,
            StrengthRate = Ratio.FromPercent(80m),
            ExpRate = Ratio.Full,
            Speed = 300,
            InitialTp = 0,
            ActionPatternType = ActionPatternType.PureRandom,
            AllyTargetRule = AllyTargetRule.PureRandom,
        },

        // 素早いが打たれ弱い。プレイヤーより先に動く（行動順が Speed で決まることの見本）
        new()
        {
            EnemyKey = BatKey,
            Name = "コウモリ",
            Rarity = Rarity.Uncommon,
            Elements = Element.Sky,
            HpShape = 70, PAtkShape = 90, PDefShape = 60, MAtkShape = 80, MDefShape = 70,
            StrengthRate = Ratio.FromPercent(90m),
            ExpRate = Ratio.FromPercent(120m),
            Speed = 700,
            InitialTp = 0,
            ActionPatternType = ActionPatternType.WeightedRandom,
            AllyTargetRule = AllyTargetRule.PureRandom,
        },

        // ローテーションで動く。順序に意味がある行動パターンの見本
        new()
        {
            EnemyKey = WolfKey,
            Name = "ウルフ",
            Rarity = Rarity.Rare,
            Elements = Element.Earth,
            HpShape = 110, PAtkShape = 130, PDefShape = 100, MAtkShape = 50, MDefShape = 80,
            StrengthRate = Ratio.FromPercent(110m),
            ExpRate = Ratio.FromPercent(180m),
            Speed = 550,
            InitialTp = 200,
            ActionPatternType = ActionPatternType.Rotation,
            AllyTargetRule = AllyTargetRule.LowestLifeRatio,
        },
    ];

    /// <summary>
    /// 敵の保有スキル。<b>通常攻撃を登録すると、その敵ではローテーションの枠を1つ占める。</b>
    /// フォールバックの通常攻撃（TPが足りないときの差し替え）とは別物である。
    /// </summary>
    public static IReadOnlyList<EnemySkillsMasterRecord> EnemySkills =>
    [
        // 重み付き。weight = 0 は本パターンでのみ「抽選対象外」を意味する
        EnemySkill(BatKey, 0, GameConstants.AttackSkillKey, 70),
        EnemySkill(BatKey, 1, "poison_bite", 30),

        // ローテーション。enemy_skill_index の昇順がそのまま順序になる。
        // 登録された通常攻撃はローテ枠を1つ占める（フォールバックの通常攻撃とは別物）
        EnemySkill(WolfKey, 0, "howl", 1),
        EnemySkill(WolfKey, 1, GameConstants.AttackSkillKey, 1),
    ];

    /// <summary>
    /// 敵の出現時 auto 付与。<c>duration_actions</c> を持たせれば
    /// 「n 行動で自滅する敵」のような表現もここから作れる。
    /// </summary>
    public static IReadOnlyList<EnemyEffectsMasterRecord> EnemyEffects =>
    [
        // 3割の確率で攻撃力が上がった個体として出現する
        new()
        {
            EnemyKey = WolfKey,
            EnemyEffectIndex = 0,
            EffectKey = AttackUpEffectKey,
            EffectRate = Ratio.FromPercent(20m),
            AttackType = null,
            DurationActions = null,
            GrantRate = Ratio.FromPercent(30m),
        },
    ];

    /// <summary>敵の装備候補。出現の都度抽選され、撃破時のドロップ候補にもなる。</summary>
    public static IReadOnlyList<EnemyEquipmentMasterRecord> EnemyEquipment =>
    [
        new()
        {
            EnemyKey = WolfKey,
            EnemyEquipmentIndex = 0,
            EquipKey = "fang_charm",
            EquipRate = Ratio.FromPercent(50m),
            DropRate = Ratio.FromPercent(10m),
        },
    ];

    public static IReadOnlyList<EnemyLootsMasterRecord> EnemyLoots =>
    [
        Loot(SlimeKey, PotionItemKey, 1, 20m),
        Loot(BatKey, AntidoteItemKey, 1, 25m),
        Loot(WolfKey, PotionItemKey, 2, 40m),
    ];

    /// <summary>撃破時の通貨。固定値であり抽選は行わない（経験値と同じ按分率が掛かる）。</summary>
    public static IReadOnlyList<EnemyCurrencyMasterRecord> EnemyCurrency =>
    [
        Currency(SlimeKey, 10),
        Currency(BatKey, 25),
        Currency(WolfKey, 80),
    ];

    // --- スキル ---

    /// <summary>
    /// スキル本体。<b>通常攻撃と防御は必ず存在しなければならない</b>
    /// （<c>SkillCatalog.Attack</c> / <c>Defend</c> が存在を前提に引く）。
    /// この2つは習得管理の対象外であり <c>chido_player_skill</c> に行を持たない。
    /// </summary>
    public static IReadOnlyList<SkillMasterRecord> Skills =>
    [
        Skill(GameConstants.AttackSkillKey, "こうげき", "対象に威力100%の無属性物理攻撃。"),

        // priority を正値にして先に動かす。軽減はそのターンの被弾に間に合う必要がある
        Skill(GameConstants.DefendSkillKey, "ぼうぎょ", "そのターンに受けるダメージを半減する。", priority: 1),

        Skill("fire_bolt", "ファイアボルト", "対象に威力130%の火属性魔法攻撃。",
            elements: Element.Fire, requireTp: 200, learnableLevel: 5),

        Skill("heal", "ヒール", "味方1体のHPを回復する。",
            requireTp: 300, learnableLevel: 10),

        Skill("cure", "キュア", "味方1体の毒を取り除く。",
            requireTp: 150, learnableLevel: 8),

        // 敵専用。learnable_level を持たせないことでレベル習得の対象から外れる
        Skill("poison_bite", "どくのキバ", "攻撃が当たると毒を付与する。", learnableLevel: null),
        Skill("howl", "とおぼえ", "自身の攻撃力を高める。", requireTp: 200, learnableLevel: null),
    ];

    /// <summary>
    /// モーション。<c>motion_index</c> 昇順に再生される。
    /// 通常攻撃・防御の <c>accuracy_rate</c> は 10000 固定（運用上の制約）。
    /// </summary>
    public static IReadOnlyList<SkillMotionMasterRecord> Motions =>
    [
        Motion(GameConstants.AttackSkillKey, 0, MotionType.Attack, TargetRule.Enemy),
        Motion(GameConstants.DefendSkillKey, 0, MotionType.GrantEffect, TargetRule.Myself),

        Motion("fire_bolt", 0, MotionType.Attack, TargetRule.Enemy, accuracy: 95m),
        Motion("heal", 0, MotionType.Heal, TargetRule.Ally),
        Motion("cure", 0, MotionType.DispelEffect, TargetRule.Ally),

        // 攻撃が効果適用に到達したときだけ毒を独立抽選する。
        // ゲートの依存先は常に先頭1件であり、直前のメンバーではない
        Motion("poison_bite", 0, MotionType.Attack, TargetRule.Enemy, accuracy: 90m, gateGroup: 1),
        Motion("poison_bite", 1, MotionType.GrantEffect, TargetRule.Enemy, accuracy: 50m, gateGroup: 1),

        Motion("howl", 0, MotionType.GrantEffect, TargetRule.Myself),
    ];

    public static IReadOnlyList<SkillMotionAttackMasterRecord> AttackMotions =>
    [
        new()
        {
            SkillKey = GameConstants.AttackSkillKey, MotionIndex = 0,
            AttackType = AttackType.Physical, Power = GameConstants.PowerScale, Elements = Element.None,
        },
        new()
        {
            SkillKey = "fire_bolt", MotionIndex = 0,
            AttackType = AttackType.Magical, Power = 130, Elements = Element.Fire,
        },
        new()
        {
            SkillKey = "poison_bite", MotionIndex = 0,
            AttackType = AttackType.Physical, Power = 70, Elements = Element.None,
        },
    ];

    /// <summary>
    /// 回復モーション。同格・威力50%の回復が同格の通常攻撃と釣り合う較正（5.1）に沿わせている。
    /// </summary>
    public static IReadOnlyList<SkillMotionHealMasterRecord> HealMotions =>
    [
        new() { SkillKey = "heal", MotionIndex = 0, AttackType = AttackType.Magical, Power = 150 },
    ];

    public static IReadOnlyList<SkillMotionEffectMasterRecord> EffectMotions =>
    [
        // 防御。fixed_rate を持つ効果であるため effect_rate は供給しない
        new()
        {
            SkillKey = GameConstants.DefendSkillKey, MotionIndex = 0,
            EffectKey = GameConstants.DefendSkillKey, EffectRate = null,
            AttackType = null, DurationActions = 1,
        },

        // 毒。SlipDamage 成分を持つため attack_type の供給が必須
        new()
        {
            SkillKey = "poison_bite", MotionIndex = 1,
            EffectKey = PoisonEffectKey, EffectRate = null,
            AttackType = AttackType.Physical, DurationActions = 3,
        },

        // 攻撃力上昇。fixed_rate を持たない不定値のため effect_rate の供給が必須
        new()
        {
            SkillKey = "howl", MotionIndex = 0,
            EffectKey = AttackUpEffectKey, EffectRate = Ratio.FromPercent(30m),
            AttackType = null, DurationActions = 3,
        },
    ];

    public static IReadOnlyList<SkillMotionDispelMasterRecord> DispelMotions =>
    [
        new() { SkillKey = "cure", MotionIndex = 0, EffectKey = PoisonEffectKey },
    ];

    // --- 状態変化 ---

    /// <summary>
    /// 状態変化マスタ。<c>effect_types</c> は非正規化キャッシュであり、
    /// <b>真実の情報源はサブテーブルの行の有無</b>（<c>EffectCatalog</c> は行から導出する）。
    /// ここでは実体と揃えて入れるが、食い違っても実行時は実体が勝つ。
    /// </summary>
    public static IReadOnlyList<EffectMasterRecord> Effects =>
    [
        new()
        {
            EffectKey = GameConstants.DefendSkillKey, Name = "防御",
            ClearOnBattleEnd = true, EffectTypes = EffectType.StatusModifier,
        },
        new()
        {
            EffectKey = PoisonEffectKey, Name = "毒",
            ClearOnBattleEnd = true, EffectTypes = EffectType.SlipDamage,
        },
        new()
        {
            EffectKey = AttackUpEffectKey, Name = "攻撃力上昇",
            ClearOnBattleEnd = true, EffectTypes = EffectType.StatusModifier,
        },
        new()
        {
            EffectKey = ParalysisEffectKey, Name = "麻痺",
            ClearOnBattleEnd = true, EffectTypes = EffectType.DisableMove,
        },
    ];

    public static IReadOnlyList<EffectStatusModifierMasterRecord> EffectStatusModifiers =>
    [
        // 固定変動。インスタンス側へ複製せずマスタが値を持つ
        new()
        {
            EffectKey = GameConstants.DefendSkillKey,
            TargetStatus = TargetStatus.DamageResistRate,
            FixedRate = GameConstants.DefendDamageResistRate,
        },

        // 不定値。付与側が effect_rate を供給する（供給が無ければ付与時に例外）
        new()
        {
            EffectKey = AttackUpEffectKey,
            TargetStatus = TargetStatus.PAtk,
            FixedRate = null,
        },
    ];

    public static IReadOnlyList<EffectSlipDamageMasterRecord> EffectSlipDamages =>
    [
        new() { EffectKey = PoisonEffectKey, Power = 25, Elements = Element.None },
    ];

    public static IReadOnlyList<EffectDisableMoveMasterRecord> EffectDisableMoves =>
    [
        new() { EffectKey = ParalysisEffectKey, DisableRate = Ratio.FromPercent(30m) },
    ];

    // --- 装備 ---

    /// <summary>
    /// 装備。1スロットの補正値 = <c>progression_value × 1.2^rarity × *_rate</c>。
    /// <c>equip_parts</c> はビット列で、複数部位に適合する装備は
    /// 空いている最小の部位へ入る（択一の候補提示）。
    /// </summary>
    public static IReadOnlyList<EquipmentMasterRecord> Equipment =>
    [
        Gear("wooden_sword", "きのつるぎ", EquipPart.Weapon, Rarity.Common, 10, pAtk: 100m),
        Gear("leather_armor", "かわのよろい", EquipPart.Chest, Rarity.Common, 10, pDef: 80m, hp: 40m),
        Gear("fang_charm", "きばのおまもり", EquipPart.Accessory1, Rarity.Uncommon, 12,
            pAtk: 40m, luck: 5m, speed: 30),
        Gear("flame_rod", "ほのおのつえ", EquipPart.Weapon, Rarity.Rare, 15,
            mAtk: 120m, elements: Element.Fire),
    ];

    // --- アイテム ---

    /// <summary>
    /// アイテム。効果は「特定スキルの発動」に収束するため、発動そのものは通常のスキル発動と
    /// 同じ経路を通る（<b>習得状況は問わない</b>）。
    /// </summary>
    public static IReadOnlyList<ItemMasterRecord> Items =>
    [
        new()
        {
            ItemKey = PotionItemKey, Name = "ポーション",
            ItemType = ItemType.Battle, IsConsumable = true,
            Description = "味方1体のHPを回復する。",
        },
        new()
        {
            ItemKey = AntidoteItemKey, Name = "どくけしそう",
            ItemType = ItemType.Battle, IsConsumable = true,
            Description = "味方1体の毒を取り除く。",
        },
    ];

    public static IReadOnlyList<ItemUsedEffectMasterRecord> ItemEffects =>
    [
        new()
        {
            ItemKey = PotionItemKey, UsageIndex = 0,
            ItemUsageType = ItemUsageType.UseSkill, SkillKey = "heal",
        },
        new()
        {
            ItemKey = AntidoteItemKey, UsageIndex = 0,
            ItemUsageType = ItemUsageType.UseSkill, SkillKey = "cure",
        },
    ];

    // --- 称号 ---

    public static IReadOnlyList<TitleMasterRecord> Titles =>
    [
        new()
        {
            TitleKey = "first_blood", Name = "はじめの一歩", Emoji = "🗡",
            AcquisitionType = TitleAcquisitionType.EnemyDefeated,
            ConditionKey = SlimeKey, ConditionValue = null,
        },
        new()
        {
            TitleKey = "veteran", Name = "歴戦", Emoji = "🎖",
            AcquisitionType = TitleAcquisitionType.LevelReached,
            ConditionKey = null, ConditionValue = new BigInteger(10),
        },
        new()
        {
            TitleKey = "collector", Name = "蒐集家", Emoji = "🧪",
            AcquisitionType = TitleAcquisitionType.ItemObtained,
            ConditionKey = PotionItemKey, ConditionValue = null,
        },
    ];

    // --- 組み立ての補助 ---

    private static FieldRarityRateMasterRecord Rate(string fieldKey, Rarity rarity, decimal percent)
        => new() { FieldKey = fieldKey, Rarity = rarity, RarityRate = Ratio.FromPercent(percent) };

    private static FieldTransitionMasterRecord Transition(string from, string to)
        => new() { FieldKey = from, NextFieldKey = to };

    private static EnemyGroupMemberMasterRecord Member(string groupKey, byte index, string enemyKey)
        => new() { GroupKey = groupKey, MemberIndex = index, EnemyKey = enemyKey };

    private static FieldEnemyGroupMasterRecord FieldGroup(string fieldKey, Rarity rarity, string groupKey)
        => new() { FieldKey = fieldKey, Rarity = rarity, GroupKey = groupKey };

    private static EnemySkillsMasterRecord EnemySkill(
        string enemyKey, byte index, string skillKey, byte weight)
        => new()
        {
            EnemyKey = enemyKey, EnemySkillIndex = index, SkillKey = skillKey, Weight = weight,
        };

    private static EnemyLootsMasterRecord Loot(
        string enemyKey, string itemKey, ushort quantity, decimal dropPercent)
        => new()
        {
            EnemyKey = enemyKey, ItemKey = itemKey, Quantity = quantity,
            DropRate = Ratio.FromPercent(dropPercent),
        };

    private static EnemyCurrencyMasterRecord Currency(string enemyKey, int amount)
        => new() { EnemyKey = enemyKey, DropAmount = new BigInteger(amount) };

    private static SkillMasterRecord Skill(
        string key,
        string name,
        string? description = null,
        Element elements = Element.None,
        ushort requireTp = 0,
        int? learnableLevel = 1,
        int priority = 0)
        => new()
        {
            SkillKey = key,
            Name = name,
            Description = description,
            Elements = elements,
            RequireTp = requireTp,
            LearnableLevel = learnableLevel is { } level ? new BigInteger(level) : null,
            Priority = priority,
            SpecialProcessKey = null,
        };

    private static SkillMotionMasterRecord Motion(
        string skillKey,
        byte index,
        MotionType type,
        TargetRule rule,
        decimal accuracy = 100m,
        ushort? gateGroup = null)
        => new()
        {
            SkillKey = skillKey,
            MotionIndex = index,
            MotionType = type,
            TargetRule = rule,
            AccuracyRate = Ratio.FromPercent(accuracy),
            AccuracyGateGroup = gateGroup,
        };

    private static EquipmentMasterRecord Gear(
        string key,
        string name,
        EquipPart parts,
        Rarity rarity,
        int progression,
        decimal hp = 0m,
        decimal pAtk = 0m,
        decimal pDef = 0m,
        decimal mAtk = 0m,
        decimal mDef = 0m,
        int speed = 0,
        decimal luck = 0m,
        Element elements = Element.None)
        => new()
        {
            EquipKey = key,
            Name = name,
            EquipParts = parts,
            Rarity = rarity,
            Elements = elements,
            ProgressionValue = new BigInteger(progression),
            HpRate = Ratio.FromPercent(hp),
            PAtkRate = Ratio.FromPercent(pAtk),
            PDefRate = Ratio.FromPercent(pDef),
            MAtkRate = Ratio.FromPercent(mAtk),
            MDefRate = Ratio.FromPercent(mDef),
            SpeedBonus = speed,
            LuckBonusRate = Ratio.FromPercent(luck),
        };

    /// <summary>Shape の基準値。個体ごとの配分はこの合計に対する割り振りとして読む。</summary>
    internal static ushort ReferenceShape => BaseShape;
}
