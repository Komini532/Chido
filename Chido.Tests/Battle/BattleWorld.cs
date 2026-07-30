using System.Numerics;
using Chido.Battle;
using Chido.Core;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Items;
using Chido.Core.Stats;
using Chido.Data;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Tests.Battle;

/// <summary>
/// 統合テスト用の最小マスタ一式。
///
/// <para>
/// 実装が要求する「必ず存在するもの」だけを投入する。草原とその <c>Common</c> の組
/// （起動時検証の対象）、通常攻撃、防御がそれにあたる。ここに書かれているものが
/// <b>Phase 10 のマスタ投入が最低限満たすべき形</b>でもある。
/// </para>
/// </summary>
public static class BattleWorld
{
    public const string EnemyKey = "test_slime";
    public const string GroupKey = "test_group";
    public const string PoisonKey = "test_poison";
    public const string ItemKey = "test_potion";
    public const string HealSkillKey = "test_heal";

    /// <summary>
    /// 複数行動にわたって残る自己強化。<b>戦闘内スコープの状態変化がコマンドをまたいで
    /// 生き残ることを確かめるためにある。</b>防御（<c>duration_actions = 1</c>）では
    /// 付与したターンの終わりに減衰で消えるため、この検証には使えない。
    /// </summary>
    public const string BuffSkillKey = "test_buff";

    public const string BuffEffectKey = "test_buff_effect";

    /// <summary>強化の持続。1ターン減衰した後も残ることを見たいので2以上にする。</summary>
    public const ushort BuffDuration = 3;

    /// <summary>
    /// 敵の素早さ。プレイヤー（<see cref="GameConstants.PlayerBaseSpeed"/>）より遅くしておく。
    /// 行動順が入れ替わると「先攻の一撃で後攻がキャンセルされる」経路に入り、
    /// 検証したい内容と無関係なところで結果が揺れる。
    /// </summary>
    private const int EnemySpeed = GameConstants.PlayerBaseSpeed - 100;

    public static async Task SeedAsync(ChidoDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.FieldMasters.AnyAsync(x => x.FieldKey == GameConstants.GrasslandFieldKey, cancellationToken))
        {
            return;
        }

        SeedField(db);
        SeedSkills(db);
        SeedEffects(db);
        SeedEnemy(db);
        SeedItem(db);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>草原とその <c>Common</c> の組。起動時検証（戦闘システム 10.5）が要求する2点。</summary>
    private static void SeedField(ChidoDbContext db)
    {
        db.FieldMasters.Add(new FieldMasterRecord
        {
            FieldKey = GameConstants.GrasslandFieldKey, Name = "草原",
        });

        db.FieldRarityRateMasters.Add(new FieldRarityRateMasterRecord
        {
            FieldKey = GameConstants.GrasslandFieldKey,
            Rarity = Rarity.Common,
            RarityRate = Ratio.Full,
        });

        db.FieldEnemyGroupMasters.Add(new FieldEnemyGroupMasterRecord
        {
            FieldKey = GameConstants.GrasslandFieldKey,
            Rarity = Rarity.Common,
            GroupKey = GroupKey,
        });

        // 自己ループ。意図的な行き止まりであり、遷移先0件による草原フォールバック（縮退）とは区別される
        db.FieldTransitionMasters.Add(new FieldTransitionMasterRecord
        {
            FieldKey = GameConstants.GrasslandFieldKey,
            NextFieldKey = GameConstants.GrasslandFieldKey,
        });

        db.EnemyGroupMasters.Add(new EnemyGroupMasterRecord { GroupKey = GroupKey, Rarity = Rarity.Common });

        db.EnemyGroupMemberMasters.Add(new EnemyGroupMemberMasterRecord
        {
            GroupKey = GroupKey, MemberIndex = 0, EnemyKey = EnemyKey,
        });
    }

    /// <summary>
    /// 通常攻撃・防御・回復スキル。
    ///
    /// 通常攻撃と防御は <see cref="GameConstants"/> のキーで引かれるため、マスタに必ず要る。
    /// 防御は自分自身への DRR 付与モーション1つで構成され、反撃モーションを含まない。
    /// </summary>
    private static void SeedSkills(ChidoDbContext db)
    {
        // 通常攻撃
        db.SkillMasters.Add(NewSkill(GameConstants.AttackSkillKey, "こうげき"));
        db.SkillMotionMasters.Add(NewMotion(GameConstants.AttackSkillKey, 0, MotionType.Attack, TargetRule.Enemy));
        db.SkillMotionAttackMasters.Add(new SkillMotionAttackMasterRecord
        {
            SkillKey = GameConstants.AttackSkillKey, MotionIndex = 0,
            AttackType = AttackType.Physical, Power = 100, Elements = Element.None,
        });

        // 防御。priority を正値にして先に動くようにするのは設計通り（戦闘システム 4.1）
        db.SkillMasters.Add(NewSkill(GameConstants.DefendSkillKey, "ぼうぎょ", priority: 1));
        db.SkillMotionMasters.Add(NewMotion(GameConstants.DefendSkillKey, 0, MotionType.GrantEffect, TargetRule.Myself));
        db.SkillMotionEffectMasters.Add(new SkillMotionEffectMasterRecord
        {
            SkillKey = GameConstants.DefendSkillKey, MotionIndex = 0,
            EffectKey = GameConstants.DefendSkillKey, EffectRate = null,
            AttackType = AttackType.Physical, DurationActions = 1,
        });

        // 習得して使う回復スキル
        db.SkillMasters.Add(NewSkill(HealSkillKey, "ヒール"));
        db.SkillMotionMasters.Add(NewMotion(HealSkillKey, 0, MotionType.Heal, TargetRule.Ally));
        db.SkillMotionHealMasters.Add(new SkillMotionHealMasterRecord
        {
            SkillKey = HealSkillKey, MotionIndex = 0, AttackType = AttackType.Magical, Power = 50,
        });

        // 複数行動にわたって残る自己強化
        db.SkillMasters.Add(NewSkill(BuffSkillKey, "きあいだめ"));
        db.SkillMotionMasters.Add(NewMotion(BuffSkillKey, 0, MotionType.GrantEffect, TargetRule.Myself));
        db.SkillMotionEffectMasters.Add(new SkillMotionEffectMasterRecord
        {
            SkillKey = BuffSkillKey, MotionIndex = 0,
            EffectKey = BuffEffectKey, EffectRate = null,
            AttackType = AttackType.Physical, DurationActions = BuffDuration,
        });
    }

    /// <summary>防御のDRRと、敵が自分に掛ける毒（auto 付与の検証用）。</summary>
    private static void SeedEffects(ChidoDbContext db)
    {
        db.EffectMasters.Add(new EffectMasterRecord
        {
            EffectKey = GameConstants.DefendSkillKey, Name = "防御",
            ClearOnBattleEnd = true, EffectTypes = EffectType.StatusModifier,
        });
        db.EffectStatusModifierMasters.Add(new EffectStatusModifierMasterRecord
        {
            EffectKey = GameConstants.DefendSkillKey,
            TargetStatus = TargetStatus.DamageResistRate,
            FixedRate = GameConstants.DefendDamageResistRate,
        });

        db.EffectMasters.Add(new EffectMasterRecord
        {
            EffectKey = PoisonKey, Name = "毒",
            ClearOnBattleEnd = true, EffectTypes = EffectType.SlipDamage,
        });
        db.EffectSlipDamageMasters.Add(new EffectSlipDamageMasterRecord
        {
            EffectKey = PoisonKey, Power = 10, Elements = Element.None,
        });

        db.EffectMasters.Add(new EffectMasterRecord
        {
            EffectKey = BuffEffectKey, Name = "気合",
            ClearOnBattleEnd = true, EffectTypes = EffectType.StatusModifier,
        });
        db.EffectStatusModifierMasters.Add(new EffectStatusModifierMasterRecord
        {
            EffectKey = BuffEffectKey,
            TargetStatus = TargetStatus.PAtk,
            FixedRate = Ratio.FromPercent(10m),
        });
    }

    private static void SeedEnemy(ChidoDbContext db)
    {
        db.EnemyMasters.Add(new EnemyMasterRecord
        {
            EnemyKey = EnemyKey,
            Name = "スライム",
            // 同格より大幅に弱くする。プレイヤーの1発で沈む必要がある（撃破 → 報酬 → 次の組の検証）
            HpShape = 10, PAtkShape = 10, PDefShape = 10, MAtkShape = 10, MDefShape = 10,
            StrengthRate = Ratio.Full,
            ExpRate = Ratio.Full,
            Speed = EnemySpeed,
            Elements = Element.None,
            InitialTp = 0,
            ActionPatternType = ActionPatternType.PureRandom,
            AllyTargetRule = AllyTargetRule.PureRandom,
        });

        db.EnemyLootsMasters.Add(new EnemyLootsMasterRecord
        {
            EnemyKey = EnemyKey, ItemKey = ItemKey, Quantity = 1, DropRate = Ratio.Full,
        });

        db.EnemyCurrencyMasters.Add(new EnemyCurrencyMasterRecord
        {
            EnemyKey = EnemyKey, DropAmount = new BigInteger(100),
        });
    }

    private static void SeedItem(ChidoDbContext db)
    {
        db.ItemMasters.Add(new ItemMasterRecord
        {
            ItemKey = ItemKey, Name = "ポーション",
            ItemType = ItemType.Battle, IsConsumable = true,
        });

        // アイテムの効果は「特定スキルの発動」に収束する
        db.ItemUsedEffectMasters.Add(new ItemUsedEffectMasterRecord
        {
            ItemKey = ItemKey, UsageIndex = 0,
            ItemUsageType = ItemUsageType.UseSkill, SkillKey = HealSkillKey,
        });
    }

    private static SkillMasterRecord NewSkill(string key, string name, int priority = 0) => new()
    {
        SkillKey = key,
        Name = name,
        Description = null,
        Elements = Element.None,
        RequireTp = 0,
        LearnableLevel = BigInteger.One,
        Priority = priority,
        SpecialProcessKey = null,
    };

    private static SkillMotionMasterRecord NewMotion(
        string key, byte index, MotionType type, TargetRule rule) => new()
    {
        SkillKey = key,
        MotionIndex = index,
        MotionType = type,
        TargetRule = rule,
        AccuracyRate = Ratio.Full,
        AccuracyGateGroup = null,
    };
}
