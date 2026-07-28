using Chido.Data.Conversions;
using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chido.Data;

/// <summary>
/// Chidoの確定スキーマ（chido-database-design.md「確定スキーマ」章）に対応するDbContext。
///
/// 暫定スキーマ（item/skill/enemy_master等）は設計未確定のためここには含めない。
/// 確定後に追加すること。
///
/// 設計書のCREATE TABLE文には明示的なFOREIGN KEY制約が一切ないため、
/// このDbContext側でもナビゲーションプロパティ・リレーションシップは定義せず、
/// スカラー列のみで表現している（＝EF Coreによる暗黙のFK自動生成を避けている）。
/// </summary>
public class ChidoDbContext(DbContextOptions<ChidoDbContext> options) : DbContext(options)
{
    public DbSet<PlayerRecord> Players => Set<PlayerRecord>();
    public DbSet<BattleStatusRecord> BattleStatuses => Set<BattleStatusRecord>();
    public DbSet<BattleSessionRecord> BattleSessions => Set<BattleSessionRecord>();
    public DbSet<BattleParticipantRecord> BattleParticipants => Set<BattleParticipantRecord>();
    public DbSet<BattleLogRecord> BattleLogs => Set<BattleLogRecord>();
    public DbSet<BattleEnemyRecord> BattleEnemies => Set<BattleEnemyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var bigInteger = new BigIntegerToStringConverter();
        var guid = new GuidToBinaryConverter();
        var nullableGuid = new NullableGuidToBinaryConverter();

        // --- 1. chido_player ---
        modelBuilder.Entity<PlayerRecord>(e =>
        {
            e.ToTable("chido_player");
            e.HasKey(x => x.UserId);

            e.Property(x => x.UserId)
                .HasColumnName("user_id")
                .ValueGeneratedNever()
                .HasComment("Discordユーザーの永続ID（スノーフレーク）");

            e.Property(x => x.UserName)
                .HasColumnName("user_name")
                .HasColumnType("VARCHAR(72)")
                .HasComment("表示名のキャッシュ。Discord APIから毎回引くとレイテンシが大きいため保持。将来的にニックネーム機能にも転用可能");
        });

        // --- 2. chido_battle_status ---
        modelBuilder.Entity<BattleStatusRecord>(e =>
        {
            e.ToTable("chido_battle_status");
            e.HasKey(x => x.UserId);

            e.Property(x => x.UserId)
                .HasColumnName("user_id")
                .ValueGeneratedNever()
                .HasComment("chido_player.user_id を参照");

            e.Property(x => x.Exp)
                .HasColumnName("exp")
                .HasColumnType("DECIMAL(65,0) UNSIGNED")
                .HasConversion(bigInteger)
                .HasComment("経験値。レベルは√expで算出。ランキング等でSQL側の比較・ソートが必要なためDECIMAL");

            e.Property(x => x.CurrentHp)
                .HasColumnName("current_hp")
                .HasColumnType("VARCHAR(100)")
                .HasConversion(bigInteger)
                .HasComment("現在HP。最大値(MaxLife)はレベルから動的算出されるためDB単体で比較する意味がなく、常にアプリ側でBigIntegerとして扱う前提の文字列型");
        });

        // --- 3. chido_battle_session ---
        modelBuilder.Entity<BattleSessionRecord>(e =>
        {
            e.ToTable("chido_battle_session");
            e.HasKey(x => x.SessionId);

            e.Property(x => x.SessionId)
                .HasColumnName("session_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(guid)
                .ValueGeneratedNever()
                .HasComment("使い捨てGuid。プレイヤーの最初の戦闘行為時に新規発行される");

            e.Property(x => x.GuildId)
                .HasColumnName("guild_id")
                .HasComment("戦闘が発生したDiscordサーバーID");

            e.Property(x => x.ChannelId)
                .HasColumnName("channel_id")
                .HasComment("戦闘が発生したチャンネルID");

            e.Property(x => x.MessageId)
                .HasColumnName("message_id")
                .HasComment("戦闘状況を表示している埋め込みメッセージのID（編集対象）");

            e.Property(x => x.LastActionAt)
                .HasColumnName("last_action_at")
                .HasColumnType("DATETIME(3)")
                .HasComment("最終行動時刻。放置タイムアウト判定に使用");

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("DATETIME(3)")
                .HasComment("セッション開始時刻");

            e.Property(x => x.EndedAt)
                .HasColumnName("ended_at")
                .HasColumnType("DATETIME(3)")
                .HasComment("終了時刻。NULL=進行中、NOT NULL=終了（phase列の代わりにこれで進行状態を表現する）");

            e.Property(x => x.EndReason)
                .HasColumnName("end_reason")
                .HasColumnType("TINYINT UNSIGNED")
                .HasConversion<byte?>()
                .HasComment("終了理由（BattleEndReason）。ended_atがNULLの間は常にNULL");
        });

        // --- 4. chido_battle_participant ---
        modelBuilder.Entity<BattleParticipantRecord>(e =>
        {
            e.ToTable("chido_battle_participant", t => t.HasCheckConstraint(
                "CK_chido_battle_participant_entity_type",
                "(entity_type = 0 AND user_id IS NOT NULL AND enemy_id IS NULL) OR " +
                "(entity_type = 1 AND user_id IS NULL AND enemy_id IS NOT NULL)"));

            e.HasKey(x => new { x.SessionId, x.EntityId });

            e.Property(x => x.SessionId)
                .HasColumnName("session_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(guid)
                .HasComment("chido_battle_session.session_id を参照");

            e.Property(x => x.EntityId)
                .HasColumnName("entity_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(guid)
                .HasComment("参加者インスタンスの使い捨てGuid（IEntity.Id）");

            e.Property(x => x.EntityType)
                .HasColumnName("entity_type")
                .HasColumnType("TINYINT UNSIGNED")
                .HasConversion<byte>()
                .HasComment("0: Player, 1: Enemy");

            e.Property(x => x.UserId)
                .HasColumnName("user_id")
                .HasComment("entity_type=0 のとき必須。chido_player.user_id を参照");

            e.Property(x => x.EnemyId)
                .HasColumnName("enemy_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(nullableGuid)
                .HasComment("entity_type=1 のとき必須。chido_battle_enemy.enemy_id を参照");

            e.Property(x => x.CurrentHp)
                .HasColumnName("current_hp")
                .HasColumnType("VARCHAR(100)")
                .HasConversion(bigInteger)
                .HasComment("戦闘中の現在HP。同時更新が起きうるため更新時はFOR UPDATEで悲観ロックする運用");

            e.Property(x => x.JoinedAt)
                .HasColumnName("joined_at")
                .HasColumnType("DATETIME(3)")
                .HasComment("参加時刻。ターン制御には使わず、Discord埋め込みでの表示順序付けにのみ使用");
        });

        // --- 5. chido_battle_log ---
        modelBuilder.Entity<BattleLogRecord>(e =>
        {
            e.ToTable("chido_battle_log");
            e.HasKey(x => x.LogId);

            e.Property(x => x.LogId)
                .HasColumnName("log_id")
                .ValueGeneratedOnAdd()
                .HasComment("ログの連番ID");

            e.Property(x => x.SessionId)
                .HasColumnName("session_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(guid)
                .HasComment("chido_battle_session.session_id を参照");

            e.Property(x => x.ActorId)
                .HasColumnName("actor_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(guid)
                .HasComment("行動主体のentity_id（chido_battle_participant.entity_id）");

            e.Property(x => x.ActionType)
                .HasColumnName("action_type")
                .HasColumnType("TINYINT UNSIGNED")
                .HasConversion<byte>()
                .HasComment("ActionType（Attack/Skill/Item/Defend/Escape）");

            e.Property(x => x.TargetId)
                .HasColumnName("target_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(nullableGuid)
                .HasComment("対象のentity_id（対象がいない行動ではNULL）");

            e.Property(x => x.Payload)
                .HasColumnName("payload")
                .HasColumnType("JSON")
                .HasComment("ダメージ量等の詳細（DamageResult等をシリアライズ）");

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("DATETIME(3)")
                .HasComment("ログ発生時刻");

            e.HasIndex(x => new { x.SessionId, x.LogId })
                .HasDatabaseName("idx_session_log");
        });

        // --- 6. chido_battle_enemy ---
        modelBuilder.Entity<BattleEnemyRecord>(e =>
        {
            e.ToTable("chido_battle_enemy");
            e.HasKey(x => x.EnemyId);

            e.Property(x => x.EnemyId)
                .HasColumnName("enemy_id")
                .HasColumnType("BINARY(16)")
                .HasConversion(guid)
                .ValueGeneratedNever()
                .HasComment("出現の都度新規発行される使い捨てGuid。1つのenemy_idにつきchido_battle_participant行は常に1つのみ");

            e.Property(x => x.MasterKey)
                .HasColumnName("master_key")
                .HasColumnType("VARCHAR(64)")
                .HasComment("chido_enemy_master.enemy_key を参照。どの敵か（種別）を示す");

            e.Property(x => x.Level)
                .HasColumnName("level")
                .HasColumnType("DECIMAL(65,0) UNSIGNED")
                .HasConversion(bigInteger)
                .HasComment("敵のレベル。基本ステータスはプレイヤー同様レベルから動的算出するためこれ以外は持たない");

            e.HasIndex(x => x.MasterKey)
                .HasDatabaseName("idx_master_key");
        });
    }
}
