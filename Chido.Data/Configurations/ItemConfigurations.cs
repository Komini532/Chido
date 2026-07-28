using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>7. chido_item_master — アイテムマスタ。</summary>
public class ItemMasterConfiguration : IEntityTypeConfiguration<ItemMasterRecord>
{
    public void Configure(EntityTypeBuilder<ItemMasterRecord> e)
    {
        e.ToTable("chido_item_master");
        e.HasKey(x => x.ItemKey);

        e.Property(x => x.ItemKey)
            .HasColumnName("item_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー");

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("VARCHAR(100)")
            .HasComment("表示名");

        e.Property(x => x.ItemType)
            .HasColumnName("item_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("アイテム種別（0: battle, 1: material, 2: collection, 3: skill_learning）。battle は戦闘ステータスに作用する戦闘用アイテムで Use アクションの対象。skill_learning は chido_item_used_effect_master 側を真実の情報源とする非正規化キャッシュ");

        e.Property(x => x.IsConsumable)
            .HasColumnName("is_consumable")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("消費アイテムか（0/1）。item_type とは独立したフラグとして持つ");

        e.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("VARCHAR(500)")
            .HasComment("説明文");

        e.Property(x => x.SpecialProcessKey)
            .HasColumnName("special_process_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("特殊処理呼び出し記号。NULL=標準処理のみで完結。値がある場合、標準処理では説明のつかない専用ロジックがアプリ側に別途存在することを示す");
    }
}

/// <summary>24. chido_item_used_effect_master — アイテム使用効果。</summary>
public class ItemUsedEffectMasterConfiguration : IEntityTypeConfiguration<ItemUsedEffectMasterRecord>
{
    public void Configure(EntityTypeBuilder<ItemUsedEffectMasterRecord> e)
    {
        e.ToTable("chido_item_used_effect_master");
        e.HasKey(x => new { x.ItemKey, x.UsageIndex });

        e.Property(x => x.ItemKey)
            .HasColumnName("item_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_item_master.item_key を参照");

        e.Property(x => x.UsageIndex)
            .HasColumnName("usage_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("効果の連番。use_skill（スキル発動）は常に1件のみ、learn_skill（スキル習得）は複数件を許容");

        e.Property(x => x.ItemUsageType)
            .HasColumnName("item_usage_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("アイテム効果種別（0: use_skill, 1: learn_skill。今後拡張予定）");

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_master.skill_key を参照。use_skill/learn_skill で使用。item_usage_type は今後拡張予定のため他の効果種別を見据えてNULL許容としている");
    }
}

/// <summary>33. chido_title_master — 称号マスタ。</summary>
public class TitleMasterConfiguration : IEntityTypeConfiguration<TitleMasterRecord>
{
    public void Configure(EntityTypeBuilder<TitleMasterRecord> e)
    {
        e.ToTable("chido_title_master");
        e.HasKey(x => x.TitleKey);

        e.Property(x => x.TitleKey)
            .HasColumnName("title_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー");

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("VARCHAR(100)")
            .HasComment("表示名");

        e.Property(x => x.Emoji)
            .HasColumnName("emoji")
            .HasColumnType("VARCHAR(64)")
            .HasComment("表示用絵文字。Unicode文字、またはDiscordカスタム絵文字の完成済みタグ文字列(<:name:id>)をそのまま格納");

        e.Property(x => x.AcquisitionType)
            .HasColumnName("acquisition_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("入手条件種別（0: 特定アイテム獲得, 1: 特定敵撃破, 2: レベル到達, 3: 所持金額到達）。今後拡張予定");

        e.Property(x => x.ConditionKey)
            .HasColumnName("condition_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("判定値(識別ID形式)。acquisition_type=0→item_key, 1→enemy_key を参照（参照先は acquisition_type により分岐）");

        e.Property(x => x.ConditionValue)
            .HasColumnName("condition_value")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.NullableNumeric)
            .HasComment("判定値(数値)。acquisition_type=2→レベル閾値、3→所持金額閾値。比較対象（exp由来のレベル、chido_player_currency.amount）と型を揃えている（いずれも VARCHAR(100)。BigIntegerToStringConverter 参照）");
    }
}
