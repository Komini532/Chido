using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>11. chido_enemy_master — 敵マスタ。</summary>
public class EnemyMasterConfiguration : IEntityTypeConfiguration<EnemyMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemyMasterRecord> e)
    {
        e.ToTable("chido_enemy_master");
        e.HasKey(x => x.EnemyKey);

        e.Property(x => x.EnemyKey)
            .HasColumnName("enemy_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー。chido_battle_enemy.master_key から参照される");

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("VARCHAR(100)")
            .HasComment("表示名");

        e.Property(x => x.ImageUrl)
            .HasColumnName("image_url")
            .HasColumnType("VARCHAR(500)")
            .HasComment("敵画像URL。Discord埋め込みに使用");

        e.Property(x => x.Rarity)
            .HasColumnName("rarity")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("レアリティ（0: Common, 1: Uncommon, 2: Rare, 3: Mythic, 4: Hidden）。個体の希少度を示す表示専用の値であり、敵の出現抽選には使用しない（抽選は chido_enemy_group_master.rarity）");

        e.Property(x => x.Elements)
            .HasColumnName("elements")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("敵本体の属性（ビット列）。0 = 属性なし。実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」で算出される。プレイヤーの本体属性は常に 0 のため対応列を持たない");

        e.Property(x => x.HpShape)
            .HasColumnName("hp_shape")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("HP Shape（種族値に相当する正規化されたステータス倍率）。1.00 を 100 として格納（permyriad ではない）。基礎ステータス = レベル × Scale（HP:12 / 攻撃・防御:8） × Shape");

        e.Property(x => x.PAtkShape)
            .HasColumnName("patk_shape")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("物理攻撃力 Shape（100=等倍）");

        e.Property(x => x.PDefShape)
            .HasColumnName("pdef_shape")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("物理防御力 Shape（100=等倍）");

        e.Property(x => x.MAtkShape)
            .HasColumnName("matk_shape")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("魔法攻撃力 Shape（100=等倍）");

        e.Property(x => x.MDefShape)
            .HasColumnName("mdef_shape")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("魔法防御力 Shape（100=等倍）");

        e.Property(x => x.StrengthRate)
            .HasColumnName("strength_rate")
            .HasColumnType("INT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("強さ倍率。permyriad（10000=等倍）。戦闘時ステータス = 基礎ステータス × 強さ倍率 × 装備補正 × 状態変化補正。プレイヤーは常に等倍");

        e.Property(x => x.ExpRate)
            .HasColumnName("exp_rate")
            .HasColumnType("INT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("経験値倍率。permyriad（10000=等倍）。strength_rate とは独立した値");

        e.Property(x => x.Speed)
            .HasColumnName("speed")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("素早さ。Scale × Shape の枠組みには含まれない固定値（プレイヤーは基本500）。変動要因は装備効果のみ");

        e.Property(x => x.InitialTp)
            .HasColumnName("initial_tp")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasDefaultValue((ushort)0)
            .HasComment("出現時の初期TP（0〜1000）。chido_battle_participant.current_tp の初期値。プレイヤーは常に0で初期化されるためこの非対称は意図的");

        e.Property(x => x.ActionPatternType)
            .HasColumnName("action_pattern_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("行動パターン（0: 完全ランダム, 1: 重み付きランダム, 2: ローテーション）。スキルの選択規則");

        e.Property(x => x.AllyTargetRule)
            .HasColumnName("ally_target_rule")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasDefaultValue(Chido.Core.Entities.Enemies.AllyTargetRule.PureRandom)
            .HasComment("味方対象モーションの対象選択規則（種族単位）。番号は族ごとに範囲を予約する（ランダム系 0-9 / 固定対象系 10-19 / 情報参照系 20-29）。現行実装は 0 / 1 / 24 の3規則のみ");
    }
}

/// <summary>12. chido_enemy_skills_master — 敵の使用スキル。</summary>
public class EnemySkillsMasterConfiguration : IEntityTypeConfiguration<EnemySkillsMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemySkillsMasterRecord> e)
    {
        e.ToTable("chido_enemy_skills_master");
        e.HasKey(x => new { x.EnemyKey, x.EnemySkillIndex });

        e.Property(x => x.EnemyKey)
            .HasColumnName("enemy_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_master.enemy_key を参照");

        e.Property(x => x.EnemySkillIndex)
            .HasColumnName("enemy_skill_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("再生・抽選順序。ローテーションの total は本テーブルの登録行数");

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_master.skill_key を参照");

        e.Property(x => x.Weight)
            .HasColumnName("weight")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("抽選の相対重み。合計値に意味を持たず Ratio への変換対象外。action_pattern_type=1 でのみ参照される。0=抽選対象外だが、完全ランダム／ローテーションでは本列自体が無視されるため通常通り使用される（意図的な非対称）");
    }
}

/// <summary>13. chido_enemy_loots_master — 敵のドロップテーブル。</summary>
public class EnemyLootsMasterConfiguration : IEntityTypeConfiguration<EnemyLootsMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemyLootsMasterRecord> e)
    {
        e.ToTable("chido_enemy_loots_master");
        e.HasKey(x => new { x.EnemyKey, x.ItemKey });

        e.Property(x => x.EnemyKey)
            .HasColumnName("enemy_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_master.enemy_key を参照");

        e.Property(x => x.ItemKey)
            .HasColumnName("item_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_item_master.item_key を参照");

        e.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("ドロップ数量");

        e.Property(x => x.DropRate)
            .HasColumnName("drop_rate")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("ドロップ率。permyriad（10000 = 100%）。判定は撃破に関与したプレイヤーごとに独立して行われる");
    }
}

/// <summary>14. chido_enemy_effects_master — 敵の初期付与状態変化。</summary>
public class EnemyEffectsMasterConfiguration : IEntityTypeConfiguration<EnemyEffectsMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemyEffectsMasterRecord> e)
    {
        e.ToTable("chido_enemy_effects_master", t => t.HasCheckConstraint(
            "CK_chido_enemy_effects_master_duration", "duration_actions IS NULL OR duration_actions >= 1"));

        e.HasKey(x => new { x.EnemyKey, x.EnemyEffectIndex });

        e.Property(x => x.EnemyKey)
            .HasColumnName("enemy_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_master.enemy_key を参照");

        e.Property(x => x.EnemyEffectIndex)
            .HasColumnName("enemy_effect_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("付与順序");

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_effect_master.effect_key を参照");

        e.Property(x => x.EffectRate)
            .HasColumnName("effect_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("効果量。permyriad、符号あり（デバフの負値を許容）。chido_skill_motion_effect_master.effect_rate と同じ性質・同じ書き込み先");

        e.Property(x => x.AttackType)
            .HasColumnName("attack_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte?>()
            .HasComment("Physical/Magical。付与する状態変化が SlipDamage 成分を持つ場合のみ NOT NULL。auto 付与の SlipDamage（「6行動で自滅する敵」等）が物理/魔法を決めるために必要");

        e.Property(x => x.DurationActions)
            .HasColumnName("duration_actions")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("持続。「残り有効行動数」であり時計ではない。NULL=無期限（敵の効果は clear_on_battle_end によらず戦闘終了時に除去される）。0 は取らない");

        e.Property(x => x.GrantRate)
            .HasColumnName("grant_rate")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("付与確率。permyriad（10000 = 100%）");

        // 同一の敵に同じ effect_key を2行定義できないようにする。これらは戦闘開始時にすべて
        // 同じ affect_reason=auto / granter=自身 / grant_source_key=NULL で付与されるため
        // 19番の重複判定キーが完全一致し、2行目以降が実行時に黙って捨てられる。
        // データ入力ミスが実行時に無言で消えるのは最悪の失敗モードであるため入力時に弾く。
        e.HasIndex(x => new { x.EnemyKey, x.EffectKey })
            .IsUnique()
            .HasDatabaseName("uk_enemy_effect");
    }
}

/// <summary>28. chido_enemy_equipment_master — 敵の装備マスタ。</summary>
public class EnemyEquipmentMasterConfiguration : IEntityTypeConfiguration<EnemyEquipmentMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemyEquipmentMasterRecord> e)
    {
        e.ToTable("chido_enemy_equipment_master");
        e.HasKey(x => new { x.EnemyKey, x.EnemyEquipmentIndex });

        e.Property(x => x.EnemyKey)
            .HasColumnName("enemy_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_master.enemy_key を参照");

        e.Property(x => x.EnemyEquipmentIndex)
            .HasColumnName("enemy_equipment_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("抽選候補の連番");

        e.Property(x => x.EquipKey)
            .HasColumnName("equip_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_equipment_master.equip_key を参照");

        e.Property(x => x.EquipRate)
            .HasColumnName("equip_rate")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("装着確率。permyriad。同一スロット内の候補の合計が 10000 未満の場合、残差は「そのスロットに装備なし」を選ぶ暗黙の重みとして扱う。超えた場合は相対比率のみの重み付き抽選へフォールバック（アプリ側の責務）");

        e.Property(x => x.DropRate)
            .HasColumnName("drop_rate")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("ドロップ率。permyriad。equip_rate とは独立した確率値");
    }
}

/// <summary>29. chido_battle_enemy_equipment — 敵の装備インスタンス（戦闘内スコープ）。</summary>
public class BattleEnemyEquipmentConfiguration : IEntityTypeConfiguration<BattleEnemyEquipmentRecord>
{
    public void Configure(EntityTypeBuilder<BattleEnemyEquipmentRecord> e)
    {
        e.ToTable("chido_battle_enemy_equipment");
        e.HasKey(x => x.InstanceId);

        e.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("使い捨てGuid。敵の出現(spawn)時、chido_enemy_equipment_master の抽選結果に基づき新規発行される");

        e.Property(x => x.EnemyId)
            .HasColumnName("enemy_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_enemy.enemy_id を参照");

        e.Property(x => x.EquipKey)
            .HasColumnName("equip_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_equipment_master.equip_key を参照");

        e.HasIndex(x => x.EnemyId)
            .HasDatabaseName("idx_enemy");
    }
}

/// <summary>30. chido_battle_enemy_equipment_slot — 敵の装着状況。</summary>
public class BattleEnemyEquipmentSlotConfiguration : IEntityTypeConfiguration<BattleEnemyEquipmentSlotRecord>
{
    public void Configure(EntityTypeBuilder<BattleEnemyEquipmentSlotRecord> e)
    {
        e.ToTable("chido_battle_enemy_equipment_slot");
        e.HasKey(x => x.EnemyId);

        e.Property(x => x.EnemyId)
            .HasColumnName("enemy_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("chido_battle_enemy.enemy_id を参照");

        e.Property(x => x.WeaponInstanceId)
            .HasColumnName("weapon_instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.NullableBinary)
            .HasComment("chido_battle_enemy_equipment.instance_id を参照");

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

/// <summary>32. chido_enemy_currency_master — 敵ドロップ金額マスタ。</summary>
public class EnemyCurrencyMasterConfiguration : IEntityTypeConfiguration<EnemyCurrencyMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemyCurrencyMasterRecord> e)
    {
        e.ToTable("chido_enemy_currency_master");
        e.HasKey(x => x.EnemyKey);

        e.Property(x => x.EnemyKey)
            .HasColumnName("enemy_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_master.enemy_key を参照");

        e.Property(x => x.DropAmount)
            .HasColumnName("drop_amount")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.Numeric)
            .HasComment("撃破時に確定でドロップする金額（固定値、抽選なし）。10進整数文字列（BigIntegerToStringConverter 参照）");
    }
}

/// <summary>42. chido_enemy_group_master — 敵の組マスタ。</summary>
public class EnemyGroupMasterConfiguration : IEntityTypeConfiguration<EnemyGroupMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemyGroupMasterRecord> e)
    {
        e.ToTable("chido_enemy_group_master");
        e.HasKey(x => x.GroupKey);

        e.Property(x => x.GroupKey)
            .HasColumnName("group_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー（例: 'slime_x3'）");

        e.Property(x => x.Rarity)
            .HasColumnName("rarity")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("組のレアリティ。敵の出現抽選およびEscape時の再抽選例外の判定は、個体ではなく組のレアリティで行う");
    }
}

/// <summary>43. chido_enemy_group_member_master — 組の構成メンバー。</summary>
public class EnemyGroupMemberMasterConfiguration : IEntityTypeConfiguration<EnemyGroupMemberMasterRecord>
{
    public void Configure(EntityTypeBuilder<EnemyGroupMemberMasterRecord> e)
    {
        e.ToTable("chido_enemy_group_member_master");
        e.HasKey(x => new { x.GroupKey, x.MemberIndex });

        e.Property(x => x.GroupKey)
            .HasColumnName("group_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_group_master.group_key を参照");

        e.Property(x => x.MemberIndex)
            .HasColumnName("member_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("出現順。chido_channel_current_enemy.spawn_index に引き継がれ、表示順とターゲット自動再選定における「先頭の敵」を決定する");

        e.Property(x => x.EnemyKey)
            .HasColumnName("enemy_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_enemy_master.enemy_key を参照");
    }
}
