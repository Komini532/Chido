using Chido.Core.Battle;
using Chido.Core.Battle.Actions;
using Chido.Core.Battle.Damage;
using Chido.Core.Battle.Effects;
using Chido.Core.Battle.Skills;
using Chido.Core.Entities;
using Chido.Core.Entities.Enemies;
using Chido.Core.Equipment;
using Chido.Core.Items;
using Chido.Core.Progression;
using Xunit;

namespace Chido.Core.Tests.Battle;

/// <summary>
/// DBへ永続化される列挙値の数値を固定する。
///
/// これらの値は TINYINT UNSIGNED / INT UNSIGNED としてそのまま格納されるため、
/// メンバーの並び替え・削除・挿入は既存行の意味を書き換えてしまう。
/// 「末尾への追加のみ」という取り決めをテストで機械的に守らせるのが本ファイルの役割であり、
/// 値の変更が意図的なものであれば、このテストの期待値も同時に更新すること。
/// </summary>
public class EnumValueTests
{
    [Fact]
    public void BattleEndReason_の値が設計と一致する()
    {
        Assert.Equal(0, (int)BattleEndReason.PlayerVictory);
        Assert.Equal(1, (int)BattleEndReason.PlayerEscaped);
        Assert.Equal(2, (int)BattleEndReason.EnemyEscaped);
        Assert.Equal(3, (int)BattleEndReason.ChannelMissing);
    }

    [Fact]
    public void ActionType_の値が設計と一致する()
    {
        Assert.Equal(0, (int)ActionType.Attack);
        Assert.Equal(1, (int)ActionType.Skill);
        Assert.Equal(2, (int)ActionType.Use);
        Assert.Equal(3, (int)ActionType.Defend);
        Assert.Equal(4, (int)ActionType.Escape);
    }

    [Fact]
    public void ParticipantStatus_の値が設計と一致する()
    {
        Assert.Equal(0, (int)ParticipantStatus.Active);
        Assert.Equal(1, (int)ParticipantStatus.Escaped);
        Assert.Equal(2, (int)ParticipantStatus.Defeated);
    }

    [Fact]
    public void EntityType_の値が設計と一致する()
    {
        Assert.Equal(0, (int)EntityType.Player);
        Assert.Equal(1, (int)EntityType.Enemy);
    }

    [Fact]
    public void AttackType_の値が設計と一致する()
    {
        Assert.Equal(0, (int)AttackType.Physical);
        Assert.Equal(1, (int)AttackType.Magical);
    }

    [Fact]
    public void MotionType_の値がサブタイプテーブルの判別子と一致する()
    {
        Assert.Equal(0, (int)MotionType.Attack);
        Assert.Equal(1, (int)MotionType.Heal);
        Assert.Equal(2, (int)MotionType.GrantEffect);
        Assert.Equal(3, (int)MotionType.Flee);
        Assert.Equal(4, (int)MotionType.DispelEffect);
    }

    [Fact]
    public void TargetRule_の値が設計と一致する()
    {
        Assert.Equal(0, (int)TargetRule.Myself);
        Assert.Equal(1, (int)TargetRule.Ally);
        Assert.Equal(2, (int)TargetRule.Enemy);
    }

    [Fact]
    public void TargetStatus_の値が設計と一致する()
    {
        Assert.Equal(0, (int)TargetStatus.MaxLife);
        Assert.Equal(1, (int)TargetStatus.PAtk);
        Assert.Equal(2, (int)TargetStatus.PDef);
        Assert.Equal(3, (int)TargetStatus.MAtk);
        Assert.Equal(4, (int)TargetStatus.MDef);
        Assert.Equal(5, (int)TargetStatus.Speed);
        Assert.Equal(6, (int)TargetStatus.Luck);
        Assert.Equal(7, (int)TargetStatus.DamageResistRate);
    }

    [Fact]
    public void AffectReason_の値が設計と一致する()
    {
        Assert.Equal(0, (int)AffectReason.Skill);
        Assert.Equal(1, (int)AffectReason.Auto);
    }

    [Fact]
    public void EffectType_はビット列である()
    {
        Assert.Equal(0, (int)EffectType.None);
        Assert.Equal(1, (int)EffectType.StatusModifier);
        Assert.Equal(2, (int)EffectType.SlipDamage);
        Assert.Equal(4, (int)EffectType.DisableMove);
        Assert.Equal(8, (int)EffectType.ElementGrant);
    }

    [Fact]
    public void Element_はビット列であり10種を持つ()
    {
        Assert.Equal(0, (int)Element.None);
        Assert.Equal(1 << 0, (int)Element.Fire);
        Assert.Equal(1 << 1, (int)Element.Water);
        Assert.Equal(1 << 2, (int)Element.Grass);
        Assert.Equal(1 << 3, (int)Element.Earth);
        Assert.Equal(1 << 4, (int)Element.Sky);
        Assert.Equal(1 << 5, (int)Element.Thunder);
        Assert.Equal(1 << 6, (int)Element.Ice);
        Assert.Equal(1 << 7, (int)Element.Light);
        Assert.Equal(1 << 8, (int)Element.Dark);
        Assert.Equal(1 << 9, (int)Element.Neutral);
    }

    [Fact]
    public void EquipPart_はビット列である()
    {
        Assert.Equal(0, (int)EquipPart.None);
        Assert.Equal(1, (int)EquipPart.Weapon);
        Assert.Equal(2, (int)EquipPart.Head);
        Assert.Equal(4, (int)EquipPart.Chest);
        Assert.Equal(8, (int)EquipPart.Legs);
        Assert.Equal(16, (int)EquipPart.Accessory1);
    }

    [Fact]
    public void Rarity_の値が設計と一致する()
    {
        Assert.Equal(0, (int)Rarity.Common);
        Assert.Equal(1, (int)Rarity.Uncommon);
        Assert.Equal(2, (int)Rarity.Rare);
        Assert.Equal(3, (int)Rarity.Mythic);
        Assert.Equal(4, (int)Rarity.Hidden);
    }

    [Fact]
    public void ActionPatternType_の値が設計と一致する()
    {
        Assert.Equal(0, (int)ActionPatternType.PureRandom);
        Assert.Equal(1, (int)ActionPatternType.WeightedRandom);
        Assert.Equal(2, (int)ActionPatternType.Rotation);
    }

    [Fact]
    public void AllyTargetRule_は族ごとに番号帯を予約している()
    {
        // ランダム系 0-9 / 固定対象系 10-19 / 情報参照系 20-29。欠番は詰めない
        Assert.Equal(0, (int)AllyTargetRule.PureRandom);
        Assert.Equal(1, (int)AllyTargetRule.RandomExceptSelf);
        Assert.Equal(10, (int)AllyTargetRule.DisplayOrder0);
        Assert.Equal(11, (int)AllyTargetRule.DisplayOrder1);
        Assert.Equal(12, (int)AllyTargetRule.DisplayOrder2);
        Assert.Equal(20, (int)AllyTargetRule.HighestPAtk);
        Assert.Equal(21, (int)AllyTargetRule.HighestMAtk);
        Assert.Equal(22, (int)AllyTargetRule.HighestPDef);
        Assert.Equal(23, (int)AllyTargetRule.HighestMDef);
        Assert.Equal(24, (int)AllyTargetRule.LowestLifeRatio);
    }

    [Fact]
    public void AllyTargetRule_の実装済みは3規則のみである()
    {
        Assert.True(AllyTargetRule.PureRandom.IsImplemented());
        Assert.True(AllyTargetRule.RandomExceptSelf.IsImplemented());
        Assert.True(AllyTargetRule.LowestLifeRatio.IsImplemented());

        // 予約値は将来拡張であり、現行では実装されていない
        Assert.False(AllyTargetRule.DisplayOrder0.IsImplemented());
        Assert.False(AllyTargetRule.DisplayOrder1.IsImplemented());
        Assert.False(AllyTargetRule.DisplayOrder2.IsImplemented());
        Assert.False(AllyTargetRule.HighestPAtk.IsImplemented());
        Assert.False(AllyTargetRule.HighestMAtk.IsImplemented());
        Assert.False(AllyTargetRule.HighestPDef.IsImplemented());
        Assert.False(AllyTargetRule.HighestMDef.IsImplemented());
    }

    [Fact]
    public void ItemType_の値が設計と一致する()
    {
        Assert.Equal(0, (int)ItemType.Battle);
        Assert.Equal(1, (int)ItemType.Material);
        Assert.Equal(2, (int)ItemType.Collection);
        Assert.Equal(3, (int)ItemType.SkillLearning);
    }

    [Fact]
    public void ItemUsageType_の値が設計と一致する()
    {
        Assert.Equal(0, (int)ItemUsageType.UseSkill);
        Assert.Equal(1, (int)ItemUsageType.LearnSkill);
    }

    [Fact]
    public void LearnedReason_の値が設計と一致する()
    {
        Assert.Equal(0, (int)LearnedReason.Level);
        Assert.Equal(1, (int)LearnedReason.Item);
        Assert.Equal(2, (int)LearnedReason.Cheat);
    }

    [Fact]
    public void TitleAcquisitionType_の値が設計と一致する()
    {
        Assert.Equal(0, (int)TitleAcquisitionType.ItemObtained);
        Assert.Equal(1, (int)TitleAcquisitionType.EnemyDefeated);
        Assert.Equal(2, (int)TitleAcquisitionType.LevelReached);
        Assert.Equal(3, (int)TitleAcquisitionType.CurrencyReached);
    }
}
