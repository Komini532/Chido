using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data;

/// <summary>
/// Chidoの確定スキーマ（chido-database-design.md「確定スキーマ」章）に対応するDbContext。
///
/// テーブルは採番1〜45番に加え、スキルモーションのサブタイプ 10a〜10d を含む計49テーブル。
/// 各テーブルの構成は Configurations/ 配下の IEntityTypeConfiguration に分割している
/// （45テーブル分を単一の OnModelCreating に並べると追跡が困難になるため）。
///
/// 設計ドキュメントのCREATE TABLE文は、スキルモーションのサブタイプ（10a〜10d）を除き
/// 明示的なFOREIGN KEY制約を持たず、コメントベースの参照で統一されている。
/// このDbContext側でもそれに倣い、ナビゲーションプロパティは 10番まわり以外に定義しない
/// （＝EF Coreによる暗黙のFK自動生成を避けている）。
/// </summary>
public class ChidoDbContext(DbContextOptions<ChidoDbContext> options) : DbContext(options)
{
    // --- プレイヤー永続 ---
    public DbSet<PlayerRecord> Players => Set<PlayerRecord>();
    public DbSet<BattleStatusRecord> BattleStatuses => Set<BattleStatusRecord>();
    public DbSet<PlayerItemRecord> PlayerItems => Set<PlayerItemRecord>();
    public DbSet<PlayerSkillRecord> PlayerSkills => Set<PlayerSkillRecord>();
    public DbSet<PlayerEquipmentRecord> PlayerEquipments => Set<PlayerEquipmentRecord>();
    public DbSet<PlayerEquipmentSlotRecord> PlayerEquipmentSlots => Set<PlayerEquipmentSlotRecord>();
    public DbSet<PlayerCurrencyRecord> PlayerCurrencies => Set<PlayerCurrencyRecord>();
    public DbSet<PlayerTitleRecord> PlayerTitles => Set<PlayerTitleRecord>();
    public DbSet<PlayerTitleDisplayRecord> PlayerTitleDisplays => Set<PlayerTitleDisplayRecord>();

    // --- 戦闘セッション ---
    public DbSet<BattleSessionRecord> BattleSessions => Set<BattleSessionRecord>();
    public DbSet<BattleParticipantRecord> BattleParticipants => Set<BattleParticipantRecord>();
    public DbSet<BattleLogRecord> BattleLogs => Set<BattleLogRecord>();
    public DbSet<BattleEnemyRecord> BattleEnemies => Set<BattleEnemyRecord>();
    public DbSet<PlayerInBattleSessionRecord> PlayerInBattleSessions => Set<PlayerInBattleSessionRecord>();

    // --- スキル ---
    public DbSet<SkillMasterRecord> SkillMasters => Set<SkillMasterRecord>();
    public DbSet<SkillMotionMasterRecord> SkillMotionMasters => Set<SkillMotionMasterRecord>();
    public DbSet<SkillMotionAttackMasterRecord> SkillMotionAttackMasters => Set<SkillMotionAttackMasterRecord>();
    public DbSet<SkillMotionHealMasterRecord> SkillMotionHealMasters => Set<SkillMotionHealMasterRecord>();
    public DbSet<SkillMotionEffectMasterRecord> SkillMotionEffectMasters => Set<SkillMotionEffectMasterRecord>();
    public DbSet<SkillMotionDispelMasterRecord> SkillMotionDispelMasters => Set<SkillMotionDispelMasterRecord>();

    // --- 敵 ---
    public DbSet<EnemyMasterRecord> EnemyMasters => Set<EnemyMasterRecord>();
    public DbSet<EnemySkillsMasterRecord> EnemySkillsMasters => Set<EnemySkillsMasterRecord>();
    public DbSet<EnemyLootsMasterRecord> EnemyLootsMasters => Set<EnemyLootsMasterRecord>();
    public DbSet<EnemyEffectsMasterRecord> EnemyEffectsMasters => Set<EnemyEffectsMasterRecord>();
    public DbSet<EnemyEquipmentMasterRecord> EnemyEquipmentMasters => Set<EnemyEquipmentMasterRecord>();
    public DbSet<BattleEnemyEquipmentRecord> BattleEnemyEquipments => Set<BattleEnemyEquipmentRecord>();
    public DbSet<BattleEnemyEquipmentSlotRecord> BattleEnemyEquipmentSlots => Set<BattleEnemyEquipmentSlotRecord>();
    public DbSet<EnemyCurrencyMasterRecord> EnemyCurrencyMasters => Set<EnemyCurrencyMasterRecord>();
    public DbSet<EnemyGroupMasterRecord> EnemyGroupMasters => Set<EnemyGroupMasterRecord>();
    public DbSet<EnemyGroupMemberMasterRecord> EnemyGroupMemberMasters => Set<EnemyGroupMemberMasterRecord>();

    // --- 状態変化 ---
    public DbSet<EffectMasterRecord> EffectMasters => Set<EffectMasterRecord>();
    public DbSet<EffectStatusModifierMasterRecord> EffectStatusModifierMasters => Set<EffectStatusModifierMasterRecord>();
    public DbSet<EffectSlipDamageMasterRecord> EffectSlipDamageMasters => Set<EffectSlipDamageMasterRecord>();
    public DbSet<EffectDisableMoveMasterRecord> EffectDisableMoveMasters => Set<EffectDisableMoveMasterRecord>();
    public DbSet<EffectElementGrantMasterRecord> EffectElementGrantMasters => Set<EffectElementGrantMasterRecord>();
    public DbSet<BattleEffectRecord> BattleEffects => Set<BattleEffectRecord>();
    public DbSet<PlayerEffectRecord> PlayerEffects => Set<PlayerEffectRecord>();
    public DbSet<EffectStatusModifierInstanceRecord> EffectStatusModifierInstances => Set<EffectStatusModifierInstanceRecord>();
    public DbSet<EffectSlipDamageInstanceRecord> EffectSlipDamageInstances => Set<EffectSlipDamageInstanceRecord>();

    // --- アイテム・装備・称号 ---
    public DbSet<ItemMasterRecord> ItemMasters => Set<ItemMasterRecord>();
    public DbSet<ItemUsedEffectMasterRecord> ItemUsedEffectMasters => Set<ItemUsedEffectMasterRecord>();
    public DbSet<EquipmentMasterRecord> EquipmentMasters => Set<EquipmentMasterRecord>();
    public DbSet<TitleMasterRecord> TitleMasters => Set<TitleMasterRecord>();

    // --- チャンネル・フィールド ---
    public DbSet<ChannelStateRecord> ChannelStates => Set<ChannelStateRecord>();
    public DbSet<ChannelCurrentEnemyRecord> ChannelCurrentEnemies => Set<ChannelCurrentEnemyRecord>();
    public DbSet<FieldMasterRecord> FieldMasters => Set<FieldMasterRecord>();
    public DbSet<FieldRarityRateMasterRecord> FieldRarityRateMasters => Set<FieldRarityRateMasterRecord>();
    public DbSet<FieldTransitionMasterRecord> FieldTransitionMasters => Set<FieldTransitionMasterRecord>();
    public DbSet<FieldEnemyGroupMasterRecord> FieldEnemyGroupMasters => Set<FieldEnemyGroupMasterRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChidoDbContext).Assembly);
    }
}
