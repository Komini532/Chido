using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>9. chido_skill_master — スキルマスタ。</summary>
public class SkillMasterConfiguration : IEntityTypeConfiguration<SkillMasterRecord>
{
    public void Configure(EntityTypeBuilder<SkillMasterRecord> e)
    {
        e.ToTable("chido_skill_master");
        e.HasKey(x => x.SkillKey);

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー");

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("VARCHAR(100)")
            .HasComment("表示名");

        e.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("VARCHAR(500)")
            .HasComment("説明文");

        e.Property(x => x.Elements)
            .HasColumnName("elements")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("スキル属性（ビット列）。ダメージ計算には一切使用しない、UI表示専用の\"見せかけ\"の値。ダメージ計算が参照するのは chido_skill_motion_attack_master.elements");

        e.Property(x => x.RequireTp)
            .HasColumnName("require_tp")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("TP消費量（0-1000）。回復モーションを含むスキルでは 200 以上とする（運用制約）。166 以下では被反撃だけでTPが自給でき回復威力の実用帯が消滅する");

        e.Property(x => x.LearnableLevel)
            .HasColumnName("learnable_level")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.NullableNumeric)
            .HasComment("習得レベル。NULL=レベルアップでは習得不可。10進整数文字列。レベル閾値の判定は C# 側で行う（BigIntegerToStringConverter 参照）");

        e.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasColumnType("INT")
            .HasDefaultValue(0)
            .HasComment("行動優先度。行動順は priority 降順 → Speed → Random。既定は 0（Attack・通常スキル）。Defend には正の値を与え、Speed に関わらず被弾前に構えを取れるようにする");

        e.Property(x => x.SpecialProcessKey)
            .HasColumnName("special_process_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("特殊処理呼び出し記号。NULL=標準の効果計算処理のみで完結");
    }
}

/// <summary>10. chido_skill_motion_master — スキルモーション（スーパータイプ）。</summary>
public class SkillMotionMasterConfiguration : IEntityTypeConfiguration<SkillMotionMasterRecord>
{
    public void Configure(EntityTypeBuilder<SkillMotionMasterRecord> e)
    {
        e.ToTable("chido_skill_motion_master");
        e.HasKey(x => new { x.SkillKey, x.MotionIndex });

        // サブタイプ側（10a〜10d）が判別子ごと参照する複合FKの参照先。
        // motion_type を含めることで、攻撃行が回復として登録される誤りをDBが弾ける。
        e.HasAlternateKey(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .HasName("uk_subtype");

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_master.skill_key を参照");

        e.Property(x => x.MotionIndex)
            .HasColumnName("motion_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("再生順序。スキルはこの昇順にモーションを再生する");

        e.Property(x => x.MotionType)
            .HasColumnName("motion_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("モーション種別。サブタイプの判別子（0: 攻撃→10a, 1: 回復→10b, 2: 状態変化付与→10c, 3: 戦闘離脱→サブタイプなし, 4: 状態変化解除→10d）");

        e.Property(x => x.TargetRule)
            .HasColumnName("target_rule")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("対象の解決規則（0: 自分自身, 1: 味方, 2: 敵）。常に単体固定。敵の味方対象モーションは chido_enemy_master.ally_target_rule で解決する");

        e.Property(x => x.AccuracyRate)
            .HasColumnName("accuracy_rate")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("命中率（攻撃・回復）／成功率（状態変化付与・解除・戦闘離脱）。permyriad。4種すべてが使用する真の共通列であるため親に置く。Attack/Defend は 10000 固定");

        e.Property(x => x.AccuracyGateGroup)
            .HasColumnName("accuracy_gate_group")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("命中の依存グループ。NULL=単独で判定。同一 skill_key 内で同値の行が1グループを成し motion_index 最小の行を先頭とする。先頭が効果適用に到達しなければ同一グループの他メンバーは抽選せずスキップ。整合性検証はアプリ側の責務");
    }
}

/// <summary>10a. chido_skill_motion_attack_master — 攻撃モーション。</summary>
public class SkillMotionAttackMasterConfiguration : IEntityTypeConfiguration<SkillMotionAttackMasterRecord>
{
    public void Configure(EntityTypeBuilder<SkillMotionAttackMasterRecord> e)
    {
        e.ToTable("chido_skill_motion_attack_master", t => t.HasCheckConstraint(
            "CK_chido_skill_motion_attack_master_motion_type", "motion_type = 0"));

        e.HasKey(x => new { x.SkillKey, x.MotionIndex });

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionIndex)
            .HasColumnName("motion_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionType)
            .HasColumnName("motion_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("0（攻撃）");

        e.Property(x => x.AttackType)
            .HasColumnName("attack_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("Physical/Magical。参照する攻撃力を選択する");

        e.Property(x => x.Power)
            .HasColumnName("power")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("威力。整数%（通常攻撃=100）。permyriad ではない点に注意。ダメージ = 攻撃力 × 威力 × 被防御係数(ATK÷(ATK+DEF))");

        e.Property(x => x.Elements)
            .HasColumnName("elements")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("モーション属性（ビット列）。攻撃モーションのみが持つ。0（属性なし）が意味を持つ既定値（相性計算をスキップ＝全属性等倍）");

        e.HasOne<SkillMotionMasterRecord>()
            .WithOne()
            .HasPrincipalKey<SkillMotionMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .HasForeignKey<SkillMotionAttackMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>10b. chido_skill_motion_heal_master — 回復モーション。</summary>
public class SkillMotionHealMasterConfiguration : IEntityTypeConfiguration<SkillMotionHealMasterRecord>
{
    public void Configure(EntityTypeBuilder<SkillMotionHealMasterRecord> e)
    {
        e.ToTable("chido_skill_motion_heal_master", t => t.HasCheckConstraint(
            "CK_chido_skill_motion_heal_master_motion_type", "motion_type = 1"));

        e.HasKey(x => new { x.SkillKey, x.MotionIndex });

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionIndex)
            .HasColumnName("motion_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionType)
            .HasColumnName("motion_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("1（回復）");

        e.Property(x => x.AttackType)
            .HasColumnName("attack_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("Physical/Magical。参照する攻撃力を選択する");

        e.Property(x => x.Power)
            .HasColumnName("power")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("威力。整数%。回復量 = 攻撃力 × 威力（対象の防御力は影響しない＝被防御係数1の攻撃）。同格では被防御係数が0.5になるため、通常攻撃(100%)と釣り合う回復は威力50%");

        e.HasOne<SkillMotionMasterRecord>()
            .WithOne()
            .HasPrincipalKey<SkillMotionMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .HasForeignKey<SkillMotionHealMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>10c. chido_skill_motion_effect_master — 状態変化付与モーション。</summary>
public class SkillMotionEffectMasterConfiguration : IEntityTypeConfiguration<SkillMotionEffectMasterRecord>
{
    public void Configure(EntityTypeBuilder<SkillMotionEffectMasterRecord> e)
    {
        e.ToTable("chido_skill_motion_effect_master", t =>
        {
            t.HasCheckConstraint("CK_chido_skill_motion_effect_master_motion_type", "motion_type = 2");
            t.HasCheckConstraint("CK_chido_skill_motion_effect_master_duration", "duration_actions IS NULL OR duration_actions >= 1");
        });

        e.HasKey(x => new { x.SkillKey, x.MotionIndex });

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionIndex)
            .HasColumnName("motion_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionType)
            .HasColumnName("motion_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("2（状態変化付与）");

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("付与する状態変化。chido_effect_master.effect_key を参照");

        e.Property(x => x.EffectRate)
            .HasColumnName("effect_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.NullablePermyriad)
            .HasComment("効果量。permyriad、符号あり（デバフの負値を許容）。付与先の fixed_rate が NULL の行に対してのみ必須。SlipDamage／DisableMove の効果量はそれぞれのマスタが持つため本列を使用しない");

        e.Property(x => x.AttackType)
            .HasColumnName("attack_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte?>()
            .HasComment("Physical/Magical。付与する状態変化が SlipDamage 成分を持つ場合に継続ダメージの基準を決める。付与時に chido_effect_slip_damage_instance.attack_type へ複製される。SlipDamage 成分を持たない付与では NULL");

        e.Property(x => x.DurationActions)
            .HasColumnName("duration_actions")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("持続。「残り有効行動数」であり時計ではない。remaining_actions の初期値として複製される。NULL=無期限、0 は取らない。付与先 effect の clear_on_battle_end=0 の場合は NOT NULL 必須（アプリ側の責務）");

        e.HasOne<SkillMotionMasterRecord>()
            .WithOne()
            .HasPrincipalKey<SkillMotionMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .HasForeignKey<SkillMotionEffectMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne<EffectMasterRecord>()
            .WithMany()
            .HasForeignKey(x => x.EffectKey)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>10d. chido_skill_motion_dispel_master — 状態変化解除モーション。</summary>
public class SkillMotionDispelMasterConfiguration : IEntityTypeConfiguration<SkillMotionDispelMasterRecord>
{
    public void Configure(EntityTypeBuilder<SkillMotionDispelMasterRecord> e)
    {
        e.ToTable("chido_skill_motion_dispel_master", t => t.HasCheckConstraint(
            "CK_chido_skill_motion_dispel_master_motion_type", "motion_type = 4"));

        e.HasKey(x => new { x.SkillKey, x.MotionIndex });

        e.Property(x => x.SkillKey)
            .HasColumnName("skill_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionIndex)
            .HasColumnName("motion_index")
            .HasColumnType("TINYINT UNSIGNED")
            .HasComment("chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）");

        e.Property(x => x.MotionType)
            .HasColumnName("motion_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("4（状態変化解除）");

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("解除対象。対象が保持する全スコープ（chido_battle_effect + chido_player_effect）から effect_key が一致する行をすべて削除する。granter_entity_id / grant_source_key / affect_reason は参照しない");

        e.HasOne<SkillMotionMasterRecord>()
            .WithOne()
            .HasPrincipalKey<SkillMotionMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .HasForeignKey<SkillMotionDispelMasterRecord>(x => new { x.SkillKey, x.MotionIndex, x.MotionType })
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne<EffectMasterRecord>()
            .WithMany()
            .HasForeignKey(x => x.EffectKey)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
