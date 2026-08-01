using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>1. chido_player — プレイヤー基本情報。</summary>
public class PlayerConfiguration : IEntityTypeConfiguration<PlayerRecord>
{
    public void Configure(EntityTypeBuilder<PlayerRecord> e)
    {
        e.ToTable("chido_player");
        e.HasKey(x => x.UserId);

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .HasComment("Discordユーザーの永続ID（スノーフレーク）。常に行が存在するため、プレイヤーに関する悲観ロックのアンカーとして使用する");

        e.Property(x => x.UserName)
            .HasColumnName("user_name")
            .HasColumnType("VARCHAR(72)")
            .HasComment("表示名のキャッシュ。Discord APIから毎回引くとレイテンシが大きいため保持。将来的にニックネーム機能にも転用可能");
    }
}

/// <summary>2. chido_battle_status — 戦闘関連ステータス。</summary>
public class BattleStatusConfiguration : IEntityTypeConfiguration<BattleStatusRecord>
{
    public void Configure(EntityTypeBuilder<BattleStatusRecord> e)
    {
        e.ToTable("chido_battle_status");
        e.HasKey(x => x.UserId);

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .HasComment("chido_player.user_id を参照");

        e.Property(x => x.Exp)
            .HasColumnName("exp")
            .HasColumnType("VARCHAR(100)")
            .HasCharSet("ascii")
            .UseCollation("ascii_bin")
            .HasConversion(Converters.Numeric)
            .HasComment("経験値。レベルは √exp で算出。初期値は 1（0 だと level=0 となり全ステータスが0になって成立しない）。10進整数文字列。ランキングは exp_len との複合インデックスで数値順を得る（DECIMAL が使えない理由は BigIntegerToStringConverter 参照）");

        e.Property(x => x.ExpLength)
            .HasColumnName("exp_len")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComputedColumnSql("CHAR_LENGTH(`exp`)", stored: true)
            .HasComment("exp の桁数。非負の正準10進文字列では (桁数, 辞書順) が数値順に一致するため、ランキングの第1ソートキーになる。DBが算出する生成列");

        // 昇順のまま張る。MySQL 8 以降は ORDER BY exp_len DESC, exp DESC のような全反転を昇順インデックスの逆走査で処理できる
        e.HasIndex(x => new { x.ExpLength, x.Exp }).HasDatabaseName("idx_exp_rank");
    }
}

/// <summary>8. chido_player_item — プレイヤー所持アイテム。</summary>
public class PlayerItemConfiguration : IEntityTypeConfiguration<PlayerItemRecord>
{
    public void Configure(EntityTypeBuilder<PlayerItemRecord> e)
    {
        e.ToTable("chido_player_item");
        e.HasKey(x => new { x.UserId, x.ItemKey });

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasComment("chido_player.user_id を参照");

        e.Property(x => x.ItemKey)
            .HasColumnName("item_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_item_master.item_key を参照");

        e.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("INT UNSIGNED")
            .HasDefaultValue(0u)
            .HasComment("所持数");
    }
}

/// <summary>23. chido_player_skill — プレイヤー習得スキル。</summary>
public class PlayerSkillConfiguration : IEntityTypeConfiguration<PlayerSkillRecord>
{
    public void Configure(EntityTypeBuilder<PlayerSkillRecord> e)
    {
        e.ToTable("chido_player_skill");
        e.HasKey(x => new { x.UserId, x.SkillKey });

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasComment("chido_player.user_id を参照");

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_master.skill_key を参照。通常攻撃（Attack）と防御（Defend）は習得管理の対象外であり行を持たない");

        e.Property(x => x.LearnedReason)
            .HasColumnName("learned_reason")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("習得理由（0: level, 1: item, 2: cheat）");
    }
}

/// <summary>26. chido_player_equipment — 装備所持状況。</summary>
public class PlayerEquipmentConfiguration : IEntityTypeConfiguration<PlayerEquipmentRecord>
{
    public void Configure(EntityTypeBuilder<PlayerEquipmentRecord> e)
    {
        e.ToTable("chido_player_equipment");
        e.HasKey(x => x.InstanceId);

        e.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("使い捨てGuid。装備を入手する都度新規発行される");

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasComment("chido_player.user_id を参照。所有者");

        e.Property(x => x.EquipKey)
            .HasColumnName("equip_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_equipment_master.equip_key を参照");

        e.HasIndex(x => new { x.UserId, x.EquipKey })
            .HasDatabaseName("idx_user_equip");
    }
}

/// <summary>27. chido_player_equipment_slot — 装備装着状況。</summary>
public class PlayerEquipmentSlotConfiguration : IEntityTypeConfiguration<PlayerEquipmentSlotRecord>
{
    public void Configure(EntityTypeBuilder<PlayerEquipmentSlotRecord> e)
    {
        e.ToTable("chido_player_equipment_slot");
        e.HasKey(x => x.UserId);

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .HasComment("chido_player.user_id を参照");

        e.Property(x => x.WeaponInstanceId)
            .HasColumnName("weapon_instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("chido_player_equipment.instance_id を参照。武器スロット");

        e.Property(x => x.HeadInstanceId)
            .HasColumnName("head_instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("頭防具スロット");

        e.Property(x => x.ChestInstanceId)
            .HasColumnName("chest_instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("胴防具スロット");

        e.Property(x => x.LegsInstanceId)
            .HasColumnName("legs_instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("脚防具スロット");

        e.Property(x => x.Accessory1InstanceId)
            .HasColumnName("accessory1_instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("アクセサリスロット1");
    }
}

/// <summary>31. chido_player_currency — プレイヤー所持金。</summary>
public class PlayerCurrencyConfiguration : IEntityTypeConfiguration<PlayerCurrencyRecord>
{
    public void Configure(EntityTypeBuilder<PlayerCurrencyRecord> e)
    {
        e.ToTable("chido_player_currency");
        e.HasKey(x => x.UserId);

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .HasComment("chido_player.user_id を参照");

        e.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("VARCHAR(100)")
            .HasCharSet("ascii")
            .UseCollation("ascii_bin")
            .HasConversion(Converters.Numeric)
            .HasComment("所持金額。10進整数文字列。ランキングは amount_len との複合インデックスで数値順を得る（chido_battle_status.exp と同じ判断基準）");

        e.Property(x => x.AmountLength)
            .HasColumnName("amount_len")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComputedColumnSql("CHAR_LENGTH(`amount`)", stored: true)
            .HasComment("amount の桁数。非負の正準10進文字列では (桁数, 辞書順) が数値順に一致するため、ランキングの第1ソートキーになる。DBが算出する生成列");

        e.HasIndex(x => new { x.AmountLength, x.Amount }).HasDatabaseName("idx_amount_rank");
    }
}

/// <summary>34. chido_player_title — 称号所持状況。</summary>
public class PlayerTitleConfiguration : IEntityTypeConfiguration<PlayerTitleRecord>
{
    public void Configure(EntityTypeBuilder<PlayerTitleRecord> e)
    {
        e.ToTable("chido_player_title");
        e.HasKey(x => new { x.UserId, x.TitleKey });

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasComment("chido_player.user_id を参照");

        e.Property(x => x.TitleKey)
            .HasColumnName("title_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_title_master.title_key を参照");
    }
}

/// <summary>35. chido_player_title_display — 表示中の称号。</summary>
public class PlayerTitleDisplayConfiguration : IEntityTypeConfiguration<PlayerTitleDisplayRecord>
{
    public void Configure(EntityTypeBuilder<PlayerTitleDisplayRecord> e)
    {
        e.ToTable("chido_player_title_display");
        e.HasKey(x => x.UserId);

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever()
            .HasComment("chido_player.user_id を参照");

        e.Property(x => x.TitleKey)
            .HasColumnName("title_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_player_title.title_key を参照。NULL=称号を表示しない");
    }
}
