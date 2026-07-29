using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>3. chido_battle_session — 戦闘セッション。</summary>
public class BattleSessionConfiguration : IEntityTypeConfiguration<BattleSessionRecord>
{
    public void Configure(EntityTypeBuilder<BattleSessionRecord> e)
    {
        e.ToTable("chido_battle_session");
        e.HasKey(x => x.SessionId);

        e.Property(x => x.SessionId)
            .HasColumnName("session_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("使い捨てGuid。プレイヤーの最初の戦闘行為時に新規発行される");

        e.Property(x => x.GuildId)
            .HasColumnName("guild_id")
            .HasComment("戦闘が発生したDiscordサーバーID");

        e.Property(x => x.ChannelId)
            .HasColumnName("channel_id")
            .HasComment("戦闘が発生したチャンネルID。chido_channel_state.channel_id を参照");

        e.Property(x => x.MessageId)
            .HasColumnName("message_id")
            .HasComment("戦闘状況を表示している埋め込みメッセージのID（編集対象）");

        // last_action_at は持たない。Timeout による強制終了が廃止され、非同期設計では長時間放置そのものが
        // 許容されるため（終了条件はチャンネルの存否）、全行動で更新しながら誰も読まない列になっていた。

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
            .HasComment("終了理由。ended_atがNULLの間は常にNULL。BattleEndReason（0: PlayerVictory, 1: PlayerEscaped, 2: EnemyEscaped, 3: ChannelMissing）");
    }
}

/// <summary>4. chido_battle_participant — 戦闘参加者。</summary>
public class BattleParticipantConfiguration : IEntityTypeConfiguration<BattleParticipantRecord>
{
    public void Configure(EntityTypeBuilder<BattleParticipantRecord> e)
    {
        e.ToTable("chido_battle_participant", t => t.HasCheckConstraint(
            "CK_chido_battle_participant_entity_type",
            "(entity_type = 0 AND user_id IS NOT NULL AND enemy_id IS NULL) OR " +
            "(entity_type = 1 AND user_id IS NULL AND enemy_id IS NOT NULL)"));

        e.HasKey(x => new { x.SessionId, x.EntityId });

        e.Property(x => x.SessionId)
            .HasColumnName("session_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_session.session_id を参照");

        e.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
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
            .HasConversion(Converters.NullableBinary)
            .HasComment("entity_type=1 のとき必須。chido_battle_enemy.enemy_id を参照");

        e.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("ParticipantStatus（0: Active, 1: Escaped, 2: Defeated）。current_hp=0 からの間接判定ではなく状態そのものを一次情報として保持する。entity_type を問わず全行に適用される");

        e.Property(x => x.CurrentHp)
            .HasColumnName("current_hp")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.Numeric)
            .HasComment("戦闘中の現在HP。現在HPの唯一の真値。参加時は MaxLife（全快）で初期化される。MaxLife を超える値を取りうる（クランプしない）。「戦闘不能」の判定には使用しない（status 列が唯一の根拠）");

        e.Property(x => x.CurrentTp)
            .HasColumnName("current_tp")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("現在のTP（0〜1000）。Player: 参加時0／Enemy: 出現時 chido_enemy_master.initial_tp で初期化。蓄積量と上限はC#側の定数として保持する");

        e.Property(x => x.CurrentTargetId)
            .HasColumnName("current_target_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("現在の攻撃対象。同一session内の他行のentity_idを参照。解決は初回既定・自動失効後の再選定を区別しない単一の導出関数で行い、結果を本列へ書き戻す。Enemy では常にNULL");

        e.Property(x => x.RotationIndex)
            .HasColumnName("rotation_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasDefaultValue((byte)0)
            .HasComment("敵のローテーション（action_pattern_type=2）の現在位置。出現時0で初期化。選択の成否に関わらず (rotation_index + 1) % total で進める。Player およびローテ以外の敵では未使用");

        e.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("表示順。entity_type ごとに独立した番号空間を持つ。Enemy: spawn_index（＝組の member_index）の恒等複製でターゲット自動再選定の根拠。Player: 参加順（MAX+1 採番）で表示にのみ使用");

        e.Property(x => x.TotalDamageDealt)
            .HasColumnName("total_damage_dealt")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.Numeric)
            .HasComment("敵参加者へ与えた実効ダメージの累計（台帳）。実効ダメージ = min(最終ダメージ, 適用直前の現在HP)。経験値按分の分子・報酬付与ゲート・分母の集計元となる共通の基準量。SlipDamage は付与者側に計上する");

        e.Property(x => x.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("DATETIME(3)")
            .HasComment("参加時刻の記録。順序付けには使用しない（display_order がその責務を持つ）");

        e.HasIndex(x => new { x.SessionId, x.EntityType, x.DisplayOrder })
            .IsUnique()
            .HasDatabaseName("uk_display_order");
    }
}

/// <summary>5. chido_battle_log — 戦闘ログ。</summary>
public class BattleLogConfiguration : IEntityTypeConfiguration<BattleLogRecord>
{
    public void Configure(EntityTypeBuilder<BattleLogRecord> e)
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
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_session.session_id を参照");

        e.Property(x => x.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("行動主体のentity_id。SlipDamage による継続ダメージでは、被害者ではなく chido_battle_effect.granter_entity_id（付与者）を記録する");

        e.Property(x => x.ActionType)
            .HasColumnName("action_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("ActionType（Attack/Skill/Use/Defend/Escape）");

        e.Property(x => x.TargetId)
            .HasColumnName("target_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("対象のentity_id（対象がいない行動ではNULL）");

        e.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("JSON")
            .HasComment("ダメージ量等の詳細（DamageResult等をシリアライズ）。記録するダメージ値は実効ダメージ ＝ min(最終ダメージ, 適用直前の現在HP)");

        e.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(3)")
            .HasComment("ログ発生時刻");

        e.HasIndex(x => new { x.SessionId, x.LogId })
            .HasDatabaseName("idx_session_log");
    }
}

/// <summary>6. chido_battle_enemy — 戦闘中の敵の状態。</summary>
public class BattleEnemyConfiguration : IEntityTypeConfiguration<BattleEnemyRecord>
{
    public void Configure(EntityTypeBuilder<BattleEnemyRecord> e)
    {
        e.ToTable("chido_battle_enemy");
        e.HasKey(x => x.EnemyId);

        e.Property(x => x.EnemyId)
            .HasColumnName("enemy_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("出現の都度新規発行される使い捨てGuid。1つのenemy_idにつきchido_battle_participant行は常に1つのみ");

        e.Property(x => x.MasterKey)
            .HasColumnName("master_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_master.enemy_key を参照。どの敵か（種別）を示す");

        e.Property(x => x.Level)
            .HasColumnName("level")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.Numeric)
            .HasComment("敵のレベル。出現時の chido_channel_state.cumulative_enemy_level をそのまま複製する。組の全メンバーが同一レベルとなる。10進整数文字列（BigIntegerToStringConverter 参照）");

        e.HasIndex(x => x.MasterKey)
            .HasDatabaseName("idx_master_key");
    }
}

/// <summary>36. chido_player_in_battle_session — 参加中の戦闘セッション。</summary>
public class PlayerInBattleSessionConfiguration : IEntityTypeConfiguration<PlayerInBattleSessionRecord>
{
    public void Configure(EntityTypeBuilder<PlayerInBattleSessionRecord> e)
    {
        e.ToTable("chido_player_in_battle_session");
        e.HasKey(x => x.UserId);

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .HasComment("chido_player.user_id を参照。1プレイヤー1行という構造により「同時参加は1セッションまで」がテーブル構造から導かれる。行の不在＝非戦闘中");

        e.Property(x => x.SessionId)
            .HasColumnName("session_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_session.session_id を参照");

        e.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_participant.entity_id を参照。(session_id, entity_id) によるPK直引きを可能にするための非正規化");

        e.HasIndex(x => x.SessionId)
            .HasDatabaseName("idx_session");
    }
}
