using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>37. chido_channel_state — チャンネル単位の永続状態。</summary>
public class ChannelStateConfiguration : IEntityTypeConfiguration<ChannelStateRecord>
{
    public void Configure(EntityTypeBuilder<ChannelStateRecord> e)
    {
        e.ToTable("chido_channel_state");
        e.HasKey(x => x.ChannelId);

        e.Property(x => x.ChannelId)
            .HasColumnName("channel_id")
            .ValueGeneratedNever()
            .HasComment("DiscordチャンネルID。行の存在自体が「このチャンネルは戦闘チャンネルである」ことを意味する。常に行が存在するため、チャンネルに関する悲観ロックのアンカーとして使用する");

        e.Property(x => x.CurrentFieldKey)
            .HasColumnName("current_field_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_field_master.field_key を参照。現在のフィールド");

        e.Property(x => x.CumulativeEnemyLevel)
            .HasColumnName("cumulative_enemy_level")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.Numeric)
            .HasComment("累積敵レベル。初期値 1。敵の組を撃破するたびに +1（減少しない）。出現する敵の level にそのまま複製される。2500 の倍数に達するたびにフィールドが切り替わる（専用カウンターは持たない）。10進整数文字列（BigIntegerToStringConverter 参照）");

        e.Property(x => x.CurrentSessionId)
            .HasColumnName("current_session_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("chido_battle_session.session_id を参照。NULL=進行中のセッションなし。1チャンネル1行という構造により「アクティブなセッションは1つ以下」が導かれ、セッション生成レースを本行のロックで直列化できる");

        e.Property(x => x.CurrentGroupKey)
            .HasColumnName("current_group_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_group_master.group_key を参照。現在出現中の組。NULL=未抽選（初期化直後）。PlayerEscaped かつ前組が Common/Uncommon の場合は同一の group_key が再出現するため、次の出現の計画に必須（戦闘システム 10.3）。出現中の敵の集合からは逆引きできない");

        e.Property(x => x.CurrentRarity)
            .HasColumnName("current_rarity")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("現在出現中の組のレアリティ。NULL=未抽選（初期化直後）。PlayerEscaped のレアリティ分岐（Rare 以上から降りたら Common へ落とす）と撃破報酬の根拠。chido_field_enemy_group_master からは同じ組が複数のフィールド・レアリティに登録されうるため逆引きできない");
    }
}

/// <summary>38. chido_channel_current_enemy — 現在出現中の敵。</summary>
public class ChannelCurrentEnemyConfiguration : IEntityTypeConfiguration<ChannelCurrentEnemyRecord>
{
    public void Configure(EntityTypeBuilder<ChannelCurrentEnemyRecord> e)
    {
        e.ToTable("chido_channel_current_enemy");
        e.HasKey(x => new { x.ChannelId, x.SpawnIndex });

        e.Property(x => x.ChannelId)
            .HasColumnName("channel_id")
            .HasComment("chido_channel_state.channel_id を参照");

        e.Property(x => x.SpawnIndex)
            .HasColumnName("spawn_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("組内の出現順。chido_enemy_group_member_master.member_index を引き継ぐ");

        e.Property(x => x.EnemyId)
            .HasColumnName("enemy_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_enemy.enemy_id を参照。書き込みは常に新規インスタンスであり「前のインスタンスを引き継ぐ」経路は存在しない");
    }
}

/// <summary>39. chido_field_master — フィールドマスタ。</summary>
public class FieldMasterConfiguration : IEntityTypeConfiguration<FieldMasterRecord>
{
    public void Configure(EntityTypeBuilder<FieldMasterRecord> e)
    {
        e.ToTable("chido_field_master");
        e.HasKey(x => x.FieldKey);

        e.Property(x => x.FieldKey)
            .HasColumnName("field_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー（例: 'grassland'）");

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("VARCHAR(100)")
            .HasComment("表示名（例: '草原'）");
    }
}

/// <summary>40. chido_field_rarity_rate_master — フィールド別レアリティ抽選率。</summary>
public class FieldRarityRateMasterConfiguration : IEntityTypeConfiguration<FieldRarityRateMasterRecord>
{
    public void Configure(EntityTypeBuilder<FieldRarityRateMasterRecord> e)
    {
        e.ToTable("chido_field_rarity_rate_master");
        e.HasKey(x => new { x.FieldKey, x.Rarity });

        e.Property(x => x.FieldKey)
            .HasColumnName("field_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_field_master.field_key を参照");

        e.Property(x => x.Rarity)
            .HasColumnName("rarity")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("レアリティ（0: Common, 1: Uncommon, 2: Rare, 3: Mythic）。Hidden(4) はイベント専用であり通常抽選の対象に一切含まれないため、行として存在させない");

        e.Property(x => x.RarityRate)
            .HasColumnName("rarity_rate")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("抽選率。permyriad。同一 field_key 内の合計が 10000 になる（残差は存在しない＝必ず1つ選ばれる）");
    }
}

/// <summary>41. chido_field_transition_master — フィールド遷移先候補。</summary>
public class FieldTransitionMasterConfiguration : IEntityTypeConfiguration<FieldTransitionMasterRecord>
{
    public void Configure(EntityTypeBuilder<FieldTransitionMasterRecord> e)
    {
        e.ToTable("chido_field_transition_master");
        e.HasKey(x => new { x.FieldKey, x.NextFieldKey });

        e.Property(x => x.FieldKey)
            .HasColumnName("field_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_field_master.field_key を参照。遷移元");

        e.Property(x => x.NextFieldKey)
            .HasColumnName("next_field_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_field_master.field_key を参照。遷移先候補。移動先は候補リストから完全ランダムで抽選するため重み列を持たない。自己参照行は「そこから動かない」という意図の明示（0件はマスタ不整合とみなし草原へフォールバック）");
    }
}

/// <summary>44. chido_field_enemy_group_master — フィールドに出現する組。</summary>
public class FieldEnemyGroupMasterConfiguration : IEntityTypeConfiguration<FieldEnemyGroupMasterRecord>
{
    public void Configure(EntityTypeBuilder<FieldEnemyGroupMasterRecord> e)
    {
        e.ToTable("chido_field_enemy_group_master");
        e.HasKey(x => new { x.FieldKey, x.GroupKey });

        e.Property(x => x.FieldKey)
            .HasColumnName("field_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_field_master.field_key を参照");

        e.Property(x => x.GroupKey)
            .HasColumnName("group_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_group_master.group_key を参照");

        e.Property(x => x.Rarity)
            .HasColumnName("rarity")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("chido_enemy_group_master.rarity の非正規化キャッシュ。「フィールドF・レアリティRの組」を単一インデックスで引くために複製する。真実の情報源は組マスタ側であり整合性の維持はアプリ側の責務");

        e.HasIndex(x => new { x.FieldKey, x.Rarity })
            .HasDatabaseName("idx_field_rarity");
    }
}
