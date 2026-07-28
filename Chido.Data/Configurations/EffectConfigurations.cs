using Chido.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chido.Data.Configurations;

/// <summary>15. chido_effect_master — 状態変化マスタ。</summary>
public class EffectMasterConfiguration : IEntityTypeConfiguration<EffectMasterRecord>
{
    public void Configure(EntityTypeBuilder<EffectMasterRecord> e)
    {
        e.ToTable("chido_effect_master");
        e.HasKey(x => x.EffectKey);

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("可読キー。10c / 10d / chido_enemy_effects_master から参照される");

        e.Property(x => x.Name)
            .HasColumnName("name")
            .HasColumnType("VARCHAR(100)")
            .HasComment("表示名");

        e.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("VARCHAR(500)")
            .HasComment("説明文");

        e.Property(x => x.EffectTypes)
            .HasColumnName("effect_types")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("保有効果種別（ビット列）。StatusModifier / SlipDamage / DisableMove / ElementGrant。各サブテーブルの行の有無に対応する非正規化キャッシュであり、真実の情報源はサブテーブル側");

        e.Property(x => x.ClearOnBattleEnd)
            .HasColumnName("clear_on_battle_end")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("戦闘終了時に解除するか（0/1）。Player: 1のとき chido_battle_effect／0のとき chido_player_effect。Enemy: 値に関わらず常に chido_battle_effect");
    }
}

/// <summary>16. chido_effect_status_modifier_master — 状態変化：ステータス変動。</summary>
public class EffectStatusModifierMasterConfiguration : IEntityTypeConfiguration<EffectStatusModifierMasterRecord>
{
    public void Configure(EntityTypeBuilder<EffectStatusModifierMasterRecord> e)
    {
        e.ToTable("chido_effect_status_modifier_master");
        e.HasKey(x => new { x.EffectKey, x.TargetStatus });

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_effect_master.effect_key を参照");

        e.Property(x => x.TargetStatus)
            .HasColumnName("target_status")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("対象ステータス。DRR（ダメージ軽減率）も本列の一値として編入する。HP/攻撃/防御を指す行は (1 + Σr) として乗算レイヤーに入るが、DRR を指す行は Σr を (10000 - Σr)/10000 の形で PostDefense に供給する（合成の意味が異なる。アプリ側で分岐）");

        e.Property(x => x.FixedRate)
            .HasColumnName("fixed_rate")
            .HasColumnType("INT")
            .HasConversion(Converters.NullablePermyriad)
            .HasComment("固定変動率。permyriad、符号あり。NOT NULL=マスタ定義の固定値（防御 Defend の DRR 50% → 5000）／NULL=不定値（適用時にインスタンス側が変動率を保持する）");
    }
}

/// <summary>17. chido_effect_slip_damage_master — 状態変化：継続ダメージ。</summary>
public class EffectSlipDamageMasterConfiguration : IEntityTypeConfiguration<EffectSlipDamageMasterRecord>
{
    public void Configure(EntityTypeBuilder<EffectSlipDamageMasterRecord> e)
    {
        e.ToTable("chido_effect_slip_damage_master");
        e.HasKey(x => x.EffectKey);

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_effect_master.effect_key を参照");

        e.Property(x => x.Elements)
            .HasColumnName("elements")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("攻撃属性（ビット列）。マスタ由来のため付与後も不変であり、スナップショット対象ではない");

        e.Property(x => x.Power)
            .HasColumnName("power")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("威力。整数%。非負。chido_skill_motion_attack_master.power と同一の概念・同一のスケール");
    }
}

/// <summary>18. chido_effect_disable_move_master — 状態変化：行動不能。</summary>
public class EffectDisableMoveMasterConfiguration : IEntityTypeConfiguration<EffectDisableMoveMasterRecord>
{
    public void Configure(EntityTypeBuilder<EffectDisableMoveMasterRecord> e)
    {
        e.ToTable("chido_effect_disable_move_master");
        e.HasKey(x => x.EffectKey);

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_effect_master.effect_key を参照");

        e.Property(x => x.DisableRate)
            .HasColumnName("disable_rate")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasConversion(Converters.Permyriad)
            .HasComment("行動不能率。permyriad（0〜10000）。付与時に固定せず、保持者が行動しようとするたびに引く確率。併存する複数インスタンスは instance_id 昇順に独立抽選し最初の成功で打ち切る");
    }
}

/// <summary>45. chido_effect_element_grant_master — 状態変化：一時的な属性付与。</summary>
public class EffectElementGrantMasterConfiguration : IEntityTypeConfiguration<EffectElementGrantMasterRecord>
{
    public void Configure(EntityTypeBuilder<EffectElementGrantMasterRecord> e)
    {
        e.ToTable("chido_effect_element_grant_master");
        e.HasKey(x => x.EffectKey);

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_effect_master.effect_key を参照");

        e.Property(x => x.Elements)
            .HasColumnName("elements")
            .HasColumnType("INT UNSIGNED")
            .HasConversion<uint>()
            .HasComment("付与する属性（ビット列）。ダメージ計算時、対象の実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」として集計される");
    }
}

/// <summary>19. chido_battle_effect — 状態変化保持（戦闘内スコープ）。</summary>
public class BattleEffectConfiguration : IEntityTypeConfiguration<BattleEffectRecord>
{
    public void Configure(EntityTypeBuilder<BattleEffectRecord> e)
    {
        e.ToTable("chido_battle_effect");
        e.HasKey(x => x.InstanceId);

        e.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("使い捨てGuid。1回の付与ごとに新規発行");

        e.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_participant.entity_id を参照。効果保持者（Player/Enemy両方あり得る）");

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_effect_master.effect_key を参照");

        e.Property(x => x.AffectReason)
            .HasColumnName("affect_reason")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("付与要因（0: skill, 1: auto）");

        e.Property(x => x.GranterEntityId)
            .HasColumnName("granter_entity_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("付与者のentity_id。auto付与時は entity_id と同値（自己付与）");

        e.Property(x => x.GrantSourceKey)
            .HasColumnName("grant_source_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("識別キー。skill付与時は skill_key。auto付与時は NULL。affect_reason は本列が「何のキーであるか」を示す型タグであり、本列からは導出できない");

        e.Property(x => x.RemainingActions)
            .HasColumnName("remaining_actions")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("残り有効行動数。付与元（10c または 14番）の duration_actions を複製して初期化する。保持者が1ターンに関与するごとに -1 し 0 で消失。NULL=無期限（SQLのNULL伝播により減衰・消失判定から自動的に外れる）");

        // 重複付与の禁止は DB の UNIQUE では守れない。判定キーの1つである grant_source_key は
        // affect_reason=auto のとき NULL を取り、MySQL の UNIQUE は NULL を互いに異なる値として
        // 扱うため NULL の行は何行でも入る。アプリ側担保はこの判定キーでは唯一の選択肢であり、
        // 比較は必ず NULL安全等価（<=>）で行うこと。
    }
}

/// <summary>20. chido_player_effect — 状態変化保持（永続スコープ）。</summary>
public class PlayerEffectConfiguration : IEntityTypeConfiguration<PlayerEffectRecord>
{
    public void Configure(EntityTypeBuilder<PlayerEffectRecord> e)
    {
        e.ToTable("chido_player_effect");
        e.HasKey(x => x.InstanceId);

        e.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("使い捨てGuid。1回の付与ごとに新規発行");

        e.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasComment("chido_player.user_id を参照。効果保持者（Playerのみ。Enemyは出現の都度使い捨てのため永続効果を持つ意味がない）");

        e.Property(x => x.EffectKey)
            .HasColumnName("effect_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("chido_effect_master.effect_key を参照");

        e.Property(x => x.AffectReason)
            .HasColumnName("affect_reason")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("付与要因（0: skill, 1: auto）");

        e.Property(x => x.GranterEntityId)
            .HasColumnName("granter_entity_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("付与時点における付与者のentity_id（履歴的参照）。重複付与の一意性判定には使用しない（セッションごとの使い捨てGuidのため、判定に含めると常に「重複ではない」となり機能しない）");

        e.Property(x => x.GrantSourceKey)
            .HasColumnName("grant_source_key")
            .HasColumnType("VARCHAR(64)")
            .HasComment("識別キー。skill付与時は skill_key。auto付与時は NULL");

        e.Property(x => x.RemainingActions)
            .HasColumnName("remaining_actions")
            .HasColumnType("SMALLINT UNSIGNED")
            .HasComment("残り有効行動数。保持者が1ターンに関与するごとに -1 し 0 で消滅。戦闘の境界では減衰も消滅もしない。NOT NULL: 永続スコープの効果は必ず有限でなければならない（終わりを保証するものが行動数しかないため）");
    }
}

/// <summary>21. chido_effect_status_modifier_instance — インスタンス側：ステータス変動。</summary>
public class EffectStatusModifierInstanceConfiguration : IEntityTypeConfiguration<EffectStatusModifierInstanceRecord>
{
    public void Configure(EntityTypeBuilder<EffectStatusModifierInstanceRecord> e)
    {
        e.ToTable("chido_effect_status_modifier_instance");
        e.HasKey(x => new { x.InstanceId, x.TargetStatus });

        e.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .HasComment("chido_battle_effect.instance_id または chido_player_effect.instance_id を参照。親が2テーブルに分かれるため FOREIGN KEY は張れない");

        e.Property(x => x.TargetStatus)
            .HasColumnName("target_status")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("chido_effect_status_modifier_master.target_status に対応");

        e.Property(x => x.Rate)
            .HasColumnName("rate")
            .HasColumnType("INT")
            .HasConversion(Converters.Permyriad)
            .HasComment("実際の変動率。permyriad、符号あり。マスタの fixed_rate が NULL の行のみここに実値を持つ。値の出所は 10c または 14番の effect_rate");
    }
}

/// <summary>22. chido_effect_slip_damage_instance — インスタンス側：継続ダメージ。</summary>
public class EffectSlipDamageInstanceConfiguration : IEntityTypeConfiguration<EffectSlipDamageInstanceRecord>
{
    public void Configure(EntityTypeBuilder<EffectSlipDamageInstanceRecord> e)
    {
        e.ToTable("chido_effect_slip_damage_instance");
        e.HasKey(x => x.InstanceId);

        e.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasColumnType("BINARY(16)")
            .HasConversion(Converters.Binary)
            .ValueGeneratedNever()
            .HasComment("chido_battle_effect.instance_id または chido_player_effect.instance_id を参照");

        e.Property(x => x.AttackType)
            .HasColumnName("attack_type")
            .HasColumnType("TINYINT UNSIGNED")
            .HasConversion<byte>()
            .HasComment("Physical/Magical。付与モーション（10c）または auto 付与（14番）から複製した静的な性質。ダメージ計算時に対象の物理/魔法DEFのどちらを引くかを決めるために保持し続ける");

        e.Property(x => x.StatusAttackValue)
            .HasColumnName("status_attack_value")
            .HasColumnType("VARCHAR(100)")
            .HasConversion(Converters.Numeric)
            .HasComment("付与時点の攻撃力実値のスナップショット。attack_type が指す側の付与者ATK（付与時の StatusModifier 込み）を格納する");
    }
}
