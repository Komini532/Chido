using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>25. chido_equipment_master — 装備マスタ。</summary>
public class EquipmentMasterConfiguration : IEntityTypeConfiguration<EquipmentMasterRecord>
{
    public void Configure(EntityTypeBuilder<EquipmentMasterRecord> e)
    {
        e.ToTable("chido_equipment_master");
        e.HasKey(x => x.EquipKey);

        e.Property(x => x.EquipKey)
            .HasColumnName("equip_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー");

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("VARCHAR(100)")
            .HasComment("表示名");

        e.Property(x => x.EquipParts)
            .HasColumnName("equip_parts")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("装備可能パーツ（ビット列。weapon/head/chest/legs/accessory）。スロットの種別（候補）を表すものであり物理カラムと1対1対応する保証はない（択一の候補提示を許容する）");

        e.Property(x => x.Rarity)
            .HasColumnName("rarity")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("装備レアリティ（0〜4）。chido_enemy_master.rarity と共通のenum。同一進行度内での強さの序列付けに使用");

        e.Property(x => x.Elements)
            .HasColumnName("elements")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("装備が付与する属性（ビット列）。0 = 属性なし。プレイヤーの本体属性は装備由来のみであり、装着中の全スロットの elements の OR で決まる");

        e.Property(x => x.ProgressionValue)
            .HasColumnName("progression_value")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.Numeric)
            .HasComment("レベルに対する想定進行度 P(level) の結果値のみを格納（例: Lv5000でP(5000)=60）。レアリティ補正(×1.2^rarity)や各ステータス補正の乗算はアプリ側で都度算出する。10進整数文字列（BigIntegerToStringConverter 参照）");

        e.Property(x => x.HpRate)
            .HasColumnName("hp_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("HP補正値。permyriad、符号あり（10000=等倍、0=このステータスに無効果、負値=デメリット装備）");

        e.Property(x => x.PAtkRate)
            .HasColumnName("patk_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("物理攻撃力補正値（同上）");

        e.Property(x => x.PDefRate)
            .HasColumnName("pdef_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("物理防御力補正値（同上）");

        e.Property(x => x.MAtkRate)
            .HasColumnName("matk_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("魔法攻撃力補正値（同上）");

        e.Property(x => x.MDefRate)
            .HasColumnName("mdef_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("魔法防御力補正値（同上）");

        e.Property(x => x.SpeedBonus)
            .HasColumnName("speed_bonus")
            .HasColumnType("INT")
            .HasComment("素早さ固定変動値。絶対値の加減算（例: +50 / -30）。Ratio への変換対象外");

        e.Property(x => x.LuckBonusRate)
            .HasColumnName("luck_bonus_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("運補正値。permyriad、符号あり。乗算ではなく%ポイントの加算（例: +5% → 500）");
    }
}
