using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chido.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_effect",
                columns: table => new
                {
                    instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "使い捨てGuid。1回の付与ごとに新規発行"),
                    entity_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_participant.entity_id を参照。効果保持者（Player/Enemy両方あり得る）"),
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    affect_reason = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "付与要因（0: skill, 1: auto）"),
                    granter_entity_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "付与者のentity_id。auto付与時は entity_id と同値（自己付与）"),
                    grant_source_key = table.Column<string>(type: "VARCHAR(64)", nullable: true, comment: "識別キー。skill付与時は skill_key。auto付与時は NULL。affect_reason は本列が「何のキーであるか」を示す型タグであり、本列からは導出できない")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    remaining_actions = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: true, comment: "残り有効行動数。付与元（10c または 14番）の duration_actions を複製して初期化する。保持者が1ターンに関与するごとに -1 し 0 で消失。NULL=無期限（SQLのNULL伝播により減衰・消失判定から自動的に外れる）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_effect", x => x.instance_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_enemy",
                columns: table => new
                {
                    enemy_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "出現の都度新規発行される使い捨てGuid。1つのenemy_idにつきchido_battle_participant行は常に1つのみ"),
                    master_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_master.enemy_key を参照。どの敵か（種別）を示す")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    level = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "敵のレベル。出現時の chido_channel_state.cumulative_enemy_level をそのまま複製する。組の全メンバーが同一レベルとなる。設計上は DECIMAL(65,0) UNSIGNED（BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_enemy", x => x.enemy_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_enemy_equipment",
                columns: table => new
                {
                    instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "使い捨てGuid。敵の出現(spawn)時、chido_enemy_equipment_master の抽選結果に基づき新規発行される"),
                    enemy_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_enemy.enemy_id を参照"),
                    equip_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_equipment_master.equip_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_enemy_equipment", x => x.instance_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_enemy_equipment_slot",
                columns: table => new
                {
                    enemy_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_enemy.enemy_id を参照"),
                    weapon_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "chido_battle_enemy_equipment.instance_id を参照"),
                    head_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "頭防具スロット"),
                    chest_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "胴防具スロット"),
                    legs_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "脚防具スロット"),
                    accessory1_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "アクセサリスロット1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_enemy_equipment_slot", x => x.enemy_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_log",
                columns: table => new
                {
                    log_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "ログの連番ID")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    session_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_session.session_id を参照"),
                    actor_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "行動主体のentity_id。SlipDamage による継続ダメージでは、被害者ではなく chido_battle_effect.granter_entity_id（付与者）を記録する"),
                    action_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "ActionType（Attack/Skill/Use/Defend/Escape）"),
                    target_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "対象のentity_id（対象がいない行動ではNULL）"),
                    payload = table.Column<string>(type: "JSON", nullable: true, comment: "ダメージ量等の詳細（DamageResult等をシリアライズ）。記録するダメージ値は実効ダメージ ＝ min(最終ダメージ, 適用直前の現在HP)")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "DATETIME(3)", nullable: false, comment: "ログ発生時刻")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_log", x => x.log_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_participant",
                columns: table => new
                {
                    session_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_session.session_id を参照"),
                    entity_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "参加者インスタンスの使い捨てGuid（IEntity.Id）"),
                    entity_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "0: Player, 1: Enemy"),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: true, comment: "entity_type=0 のとき必須。chido_player.user_id を参照"),
                    enemy_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "entity_type=1 のとき必須。chido_battle_enemy.enemy_id を参照"),
                    status = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "ParticipantStatus（0: Active, 1: Escaped, 2: Defeated）。current_hp=0 からの間接判定ではなく状態そのものを一次情報として保持する。entity_type を問わず全行に適用される"),
                    current_hp = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "戦闘中の現在HP。現在HPの唯一の真値。参加時は MaxLife（全快）で初期化される。MaxLife を超える値を取りうる（クランプしない）。「戦闘不能」の判定には使用しない（status 列が唯一の根拠）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    current_tp = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "現在のTP（0〜1000）。Player: 参加時0／Enemy: 出現時 chido_enemy_master.initial_tp で初期化。蓄積量と上限はC#側の定数として保持する"),
                    current_target_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "現在の攻撃対象。同一session内の他行のentity_idを参照。解決は初回既定・自動失効後の再選定を区別しない単一の導出関数で行い、結果を本列へ書き戻す。Enemy では常にNULL"),
                    rotation_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, defaultValue: (byte)0, comment: "敵のローテーション（action_pattern_type=2）の現在位置。出現時0で初期化。選択の成否に関わらず (rotation_index + 1) % total で進める。Player およびローテ以外の敵では未使用"),
                    display_order = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "表示順。entity_type ごとに独立した番号空間を持つ。Enemy: spawn_index（＝組の member_index）の恒等複製でターゲット自動再選定の根拠。Player: 参加順（MAX+1 採番）で表示にのみ使用"),
                    total_damage_dealt = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "敵参加者へ与えた実効ダメージの累計（台帳）。実効ダメージ = min(最終ダメージ, 適用直前の現在HP)。経験値按分の分子・報酬付与ゲート・分母の集計元となる共通の基準量。SlipDamage は付与者側に計上する")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    joined_at = table.Column<DateTime>(type: "DATETIME(3)", nullable: false, comment: "参加時刻の記録。順序付けには使用しない（display_order がその責務を持つ）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_participant", x => new { x.session_id, x.entity_id });
                    table.CheckConstraint("CK_chido_battle_participant_entity_type", "(entity_type = 0 AND user_id IS NOT NULL AND enemy_id IS NULL) OR (entity_type = 1 AND user_id IS NULL AND enemy_id IS NOT NULL)");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_session",
                columns: table => new
                {
                    session_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "使い捨てGuid。プレイヤーの最初の戦闘行為時に新規発行される"),
                    guild_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "戦闘が発生したDiscordサーバーID"),
                    channel_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "戦闘が発生したチャンネルID。chido_channel_state.channel_id を参照"),
                    message_id = table.Column<ulong>(type: "bigint unsigned", nullable: true, comment: "戦闘状況を表示している埋め込みメッセージのID（編集対象）"),
                    created_at = table.Column<DateTime>(type: "DATETIME(3)", nullable: false, comment: "セッション開始時刻"),
                    ended_at = table.Column<DateTime>(type: "DATETIME(3)", nullable: true, comment: "終了時刻。NULL=進行中、NOT NULL=終了（phase列の代わりにこれで進行状態を表現する）"),
                    end_reason = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: true, comment: "終了理由。ended_atがNULLの間は常にNULL。BattleEndReason（0: PlayerVictory, 1: PlayerEscaped, 2: EnemyEscaped, 3: ChannelMissing）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_session", x => x.session_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_battle_status",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照"),
                    exp = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "経験値。レベルは √exp で算出。初期値は 1（0 だと level=0 となり全ステータスが0になって成立しない）。設計上はランキング用に DECIMAL(65,0) だが、BigInteger を DECIMAL 列へマップできないため VARCHAR(100)（BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_battle_status", x => x.user_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_channel_current_enemy",
                columns: table => new
                {
                    channel_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_channel_state.channel_id を参照"),
                    spawn_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "組内の出現順。chido_enemy_group_member_master.member_index を引き継ぐ"),
                    enemy_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_enemy.enemy_id を参照。書き込みは常に新規インスタンスであり「前のインスタンスを引き継ぐ」経路は存在しない")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_channel_current_enemy", x => new { x.channel_id, x.spawn_index });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_channel_state",
                columns: table => new
                {
                    channel_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "DiscordチャンネルID。行の存在自体が「このチャンネルは戦闘チャンネルである」ことを意味する。常に行が存在するため、チャンネルに関する悲観ロックのアンカーとして使用する"),
                    current_field_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_field_master.field_key を参照。現在のフィールド")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cumulative_enemy_level = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "累積敵レベル。初期値 1。敵の組を撃破するたびに +1（減少しない）。出現する敵の level にそのまま複製される。2500 の倍数に達するたびにフィールドが切り替わる（専用カウンターは持たない）。設計上は DECIMAL(65,0) UNSIGNED（BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    current_session_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "chido_battle_session.session_id を参照。NULL=進行中のセッションなし。1チャンネル1行という構造により「アクティブなセッションは1つ以下」が導かれ、セッション生成レースを本行のロックで直列化できる")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_channel_state", x => x.channel_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_effect_disable_move_master",
                columns: table => new
                {
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    disable_rate = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "行動不能率。permyriad（0〜10000）。付与時に固定せず、保持者が行動しようとするたびに引く確率。併存する複数インスタンスは instance_id 昇順に独立抽選し最初の成功で打ち切る")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_effect_disable_move_master", x => x.effect_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_effect_element_grant_master",
                columns: table => new
                {
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    elements = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "付与する属性（ビット列）。ダメージ計算時、対象の実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」として集計される")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_effect_element_grant_master", x => x.effect_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_effect_master",
                columns: table => new
                {
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー。10c / 10d / chido_enemy_effects_master から参照される")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "表示名")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "VARCHAR(500)", nullable: true, comment: "説明文")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    effect_types = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "保有効果種別（ビット列）。StatusModifier / SlipDamage / DisableMove / ElementGrant。各サブテーブルの行の有無に対応する非正規化キャッシュであり、真実の情報源はサブテーブル側"),
                    clear_on_battle_end = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "戦闘終了時に解除するか（0/1）。Player: 1のとき chido_battle_effect／0のとき chido_player_effect。Enemy: 値に関わらず常に chido_battle_effect")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_effect_master", x => x.effect_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_effect_slip_damage_instance",
                columns: table => new
                {
                    instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_effect.instance_id または chido_player_effect.instance_id を参照"),
                    attack_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "Physical/Magical。付与モーション（10c）または auto 付与（14番）から複製した静的な性質。ダメージ計算時に対象の物理/魔法DEFのどちらを引くかを決めるために保持し続ける"),
                    status_attack_value = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "付与時点の攻撃力実値のスナップショット。attack_type が指す側の付与者ATK（付与時の StatusModifier 込み）を格納する")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_effect_slip_damage_instance", x => x.instance_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_effect_slip_damage_master",
                columns: table => new
                {
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    elements = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "攻撃属性（ビット列）。マスタ由来のため付与後も不変であり、スナップショット対象ではない"),
                    power = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "威力。整数%。非負。chido_skill_motion_attack_master.power と同一の概念・同一のスケール")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_effect_slip_damage_master", x => x.effect_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_effect_status_modifier_instance",
                columns: table => new
                {
                    instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_effect.instance_id または chido_player_effect.instance_id を参照。親が2テーブルに分かれるため FOREIGN KEY は張れない"),
                    target_status = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "chido_effect_status_modifier_master.target_status に対応"),
                    rate = table.Column<int>(type: "INT", nullable: false, comment: "実際の変動率。permyriad、符号あり。マスタの fixed_rate が NULL の行のみここに実値を持つ。値の出所は 10c または 14番の effect_rate")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_effect_status_modifier_instance", x => new { x.instance_id, x.target_status });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_effect_status_modifier_master",
                columns: table => new
                {
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    target_status = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "対象ステータス。DRR（ダメージ軽減率）も本列の一値として編入する。HP/攻撃/防御を指す行は (1 + Σr) として乗算レイヤーに入るが、DRR を指す行は Σr を (10000 - Σr)/10000 の形で PostDefense に供給する（合成の意味が異なる。アプリ側で分岐）"),
                    fixed_rate = table.Column<int>(type: "INT", nullable: true, comment: "固定変動率。permyriad、符号あり。NOT NULL=マスタ定義の固定値（防御 Defend の DRR 50% → 5000）／NULL=不定値（適用時にインスタンス側が変動率を保持する）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_effect_status_modifier_master", x => new { x.effect_key, x.target_status });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_currency_master",
                columns: table => new
                {
                    enemy_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_master.enemy_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    drop_amount = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "撃破時に確定でドロップする金額（固定値、抽選なし）。設計上は DECIMAL(65,0) UNSIGNED だが、BigInteger を DECIMAL 列へマップできないため VARCHAR(100)（BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_currency_master", x => x.enemy_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_effects_master",
                columns: table => new
                {
                    enemy_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_master.enemy_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enemy_effect_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "付与順序"),
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    effect_rate = table.Column<int>(type: "INT", nullable: false, comment: "効果量。permyriad、符号あり（デバフの負値を許容）。chido_skill_motion_effect_master.effect_rate と同じ性質・同じ書き込み先"),
                    attack_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: true, comment: "Physical/Magical。付与する状態変化が SlipDamage 成分を持つ場合のみ NOT NULL。auto 付与の SlipDamage（「6行動で自滅する敵」等）が物理/魔法を決めるために必要"),
                    duration_actions = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: true, comment: "持続。「残り有効行動数」であり時計ではない。NULL=無期限（敵の効果は clear_on_battle_end によらず戦闘終了時に除去される）。0 は取らない"),
                    grant_rate = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "付与確率。permyriad（10000 = 100%）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_effects_master", x => new { x.enemy_key, x.enemy_effect_index });
                    table.CheckConstraint("CK_chido_enemy_effects_master_duration", "duration_actions IS NULL OR duration_actions >= 1");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_equipment_master",
                columns: table => new
                {
                    enemy_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_master.enemy_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enemy_equipment_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "抽選候補の連番"),
                    equip_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_equipment_master.equip_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    equip_rate = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "装着確率。permyriad。同一スロット内の候補の合計が 10000 未満の場合、残差は「そのスロットに装備なし」を選ぶ暗黙の重みとして扱う。超えた場合は相対比率のみの重み付き抽選へフォールバック（アプリ側の責務）"),
                    drop_rate = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "ドロップ率。permyriad。equip_rate とは独立した確率値")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_equipment_master", x => new { x.enemy_key, x.enemy_equipment_index });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_group_master",
                columns: table => new
                {
                    group_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー（例: 'slime_x3'）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rarity = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "組のレアリティ。敵の出現抽選およびEscape時の再抽選例外の判定は、個体ではなく組のレアリティで行う")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_group_master", x => x.group_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_group_member_master",
                columns: table => new
                {
                    group_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_group_master.group_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    member_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "出現順。chido_channel_current_enemy.spawn_index に引き継がれ、表示順とターゲット自動再選定における「先頭の敵」を決定する"),
                    enemy_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_master.enemy_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_group_member_master", x => new { x.group_key, x.member_index });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_loots_master",
                columns: table => new
                {
                    enemy_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_master.enemy_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    item_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_item_master.item_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "ドロップ数量"),
                    drop_rate = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "ドロップ率。permyriad（10000 = 100%）。判定は撃破に関与したプレイヤーごとに独立して行われる")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_loots_master", x => new { x.enemy_key, x.item_key });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_master",
                columns: table => new
                {
                    enemy_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー。chido_battle_enemy.master_key から参照される")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "表示名")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    image_url = table.Column<string>(type: "VARCHAR(500)", nullable: true, comment: "敵画像URL。Discord埋め込みに使用")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rarity = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "レアリティ（0: Common, 1: Uncommon, 2: Rare, 3: Mythic, 4: Hidden）。個体の希少度を示す表示専用の値であり、敵の出現抽選には使用しない（抽選は chido_enemy_group_master.rarity）"),
                    elements = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "敵本体の属性（ビット列）。0 = 属性なし。実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」で算出される。プレイヤーの本体属性は常に 0 のため対応列を持たない"),
                    hp_shape = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "HP Shape（種族値に相当する正規化されたステータス倍率）。1.00 を 100 として格納（permyriad ではない）。基礎ステータス = レベル × Scale（HP:12 / 攻撃・防御:8） × Shape"),
                    patk_shape = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "物理攻撃力 Shape（100=等倍）"),
                    pdef_shape = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "物理防御力 Shape（100=等倍）"),
                    matk_shape = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "魔法攻撃力 Shape（100=等倍）"),
                    mdef_shape = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "魔法防御力 Shape（100=等倍）"),
                    strength_rate = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "強さ倍率。permyriad（10000=等倍）。戦闘時ステータス = 基礎ステータス × 強さ倍率 × 装備補正 × 状態変化補正。プレイヤーは常に等倍"),
                    exp_rate = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "経験値倍率。permyriad（10000=等倍）。strength_rate とは独立した値"),
                    speed = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "素早さ。Scale × Shape の枠組みには含まれない固定値（プレイヤーは基本500）。変動要因は装備効果のみ"),
                    initial_tp = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, defaultValue: (ushort)0, comment: "出現時の初期TP（0〜1000）。chido_battle_participant.current_tp の初期値。プレイヤーは常に0で初期化されるためこの非対称は意図的"),
                    action_pattern_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "行動パターン（0: 完全ランダム, 1: 重み付きランダム, 2: ローテーション）。スキルの選択規則"),
                    ally_target_rule = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, defaultValue: (byte)0, comment: "味方対象モーションの対象選択規則（種族単位）。番号は族ごとに範囲を予約する（ランダム系 0-9 / 固定対象系 10-19 / 情報参照系 20-29）。現行実装は 0 / 1 / 24 の3規則のみ")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_master", x => x.enemy_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_enemy_skills_master",
                columns: table => new
                {
                    enemy_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_master.enemy_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    enemy_skill_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "再生・抽選順序。ローテーションの total は本テーブルの登録行数"),
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_skill_master.skill_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    weight = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "抽選の相対重み。合計値に意味を持たず Ratio への変換対象外。action_pattern_type=1 でのみ参照される。0=抽選対象外だが、完全ランダム／ローテーションでは本列自体が無視されるため通常通り使用される（意図的な非対称）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_enemy_skills_master", x => new { x.enemy_key, x.enemy_skill_index });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_equipment_master",
                columns: table => new
                {
                    equip_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "表示名")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    equip_parts = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "装備可能パーツ（ビット列。weapon/head/chest/legs/accessory）。スロットの種別（候補）を表すものであり物理カラムと1対1対応する保証はない（択一の候補提示を許容する）"),
                    rarity = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "装備レアリティ（0〜4）。chido_enemy_master.rarity と共通のenum。同一進行度内での強さの序列付けに使用"),
                    elements = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "装備が付与する属性（ビット列）。0 = 属性なし。プレイヤーの本体属性は装備由来のみであり、装着中の全スロットの elements の OR で決まる"),
                    progression_value = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "レベルに対する想定進行度 P(level) の結果値のみを格納（例: Lv5000でP(5000)=60）。レアリティ補正(×1.2^rarity)や各ステータス補正の乗算はアプリ側で都度算出する。設計上は DECIMAL(65,0) UNSIGNED（BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hp_rate = table.Column<int>(type: "INT", nullable: false, comment: "HP補正値。permyriad、符号あり（10000=等倍、0=このステータスに無効果、負値=デメリット装備）"),
                    patk_rate = table.Column<int>(type: "INT", nullable: false, comment: "物理攻撃力補正値（同上）"),
                    pdef_rate = table.Column<int>(type: "INT", nullable: false, comment: "物理防御力補正値（同上）"),
                    matk_rate = table.Column<int>(type: "INT", nullable: false, comment: "魔法攻撃力補正値（同上）"),
                    mdef_rate = table.Column<int>(type: "INT", nullable: false, comment: "魔法防御力補正値（同上）"),
                    speed_bonus = table.Column<int>(type: "INT", nullable: false, comment: "素早さ固定変動値。絶対値の加減算（例: +50 / -30）。Ratio への変換対象外"),
                    luck_bonus_rate = table.Column<int>(type: "INT", nullable: false, comment: "運補正値。permyriad、符号あり。乗算ではなく%ポイントの加算（例: +5% → 500）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_equipment_master", x => x.equip_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_field_enemy_group_master",
                columns: table => new
                {
                    field_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_field_master.field_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    group_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_enemy_group_master.group_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rarity = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "chido_enemy_group_master.rarity の非正規化キャッシュ。「フィールドF・レアリティRの組」を単一インデックスで引くために複製する。真実の情報源は組マスタ側であり整合性の維持はアプリ側の責務")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_field_enemy_group_master", x => new { x.field_key, x.group_key });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_field_master",
                columns: table => new
                {
                    field_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー（例: 'grassland'）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "表示名（例: '草原'）")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_field_master", x => x.field_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_field_rarity_rate_master",
                columns: table => new
                {
                    field_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_field_master.field_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rarity = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "レアリティ（0: Common, 1: Uncommon, 2: Rare, 3: Mythic）。Hidden(4) はイベント専用であり通常抽選の対象に一切含まれないため、行として存在させない"),
                    rarity_rate = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "抽選率。permyriad。同一 field_key 内の合計が 10000 になる（残差は存在しない＝必ず1つ選ばれる）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_field_rarity_rate_master", x => new { x.field_key, x.rarity });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_field_transition_master",
                columns: table => new
                {
                    field_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_field_master.field_key を参照。遷移元")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    next_field_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_field_master.field_key を参照。遷移先候補。移動先は候補リストから完全ランダムで抽選するため重み列を持たない。自己参照行は「そこから動かない」という意図の明示（0件はマスタ不整合とみなし草原へフォールバック）")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_field_transition_master", x => new { x.field_key, x.next_field_key });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_item_master",
                columns: table => new
                {
                    item_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "表示名")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    item_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "アイテム種別（0: battle, 1: material, 2: collection, 3: skill_learning）。battle は戦闘ステータスに作用する戦闘用アイテムで Use アクションの対象。skill_learning は chido_item_used_effect_master 側を真実の情報源とする非正規化キャッシュ"),
                    is_consumable = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "消費アイテムか（0/1）。item_type とは独立したフラグとして持つ"),
                    description = table.Column<string>(type: "VARCHAR(500)", nullable: true, comment: "説明文")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    special_process_key = table.Column<string>(type: "VARCHAR(64)", nullable: true, comment: "特殊処理呼び出し記号。NULL=標準処理のみで完結。値がある場合、標準処理では説明のつかない専用ロジックがアプリ側に別途存在することを示す")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_item_master", x => x.item_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_item_used_effect_master",
                columns: table => new
                {
                    item_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_item_master.item_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    usage_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "効果の連番。use_skill（スキル発動）は常に1件のみ、learn_skill（スキル習得）は複数件を許容"),
                    item_usage_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "アイテム効果種別（0: use_skill, 1: learn_skill。今後拡張予定）"),
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: true, comment: "chido_skill_master.skill_key を参照。use_skill/learn_skill で使用。item_usage_type は今後拡張予定のため他の効果種別を見据えてNULL許容としている")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_item_used_effect_master", x => new { x.item_key, x.usage_index });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "Discordユーザーの永続ID（スノーフレーク）。常に行が存在するため、プレイヤーに関する悲観ロックのアンカーとして使用する"),
                    user_name = table.Column<string>(type: "VARCHAR(72)", nullable: true, comment: "表示名のキャッシュ。Discord APIから毎回引くとレイテンシが大きいため保持。将来的にニックネーム機能にも転用可能")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player", x => x.user_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_currency",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照"),
                    amount = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "所持金額。設計上はランキング用に DECIMAL(65,0) UNSIGNED だが、BigInteger を DECIMAL 列へマップできないため VARCHAR(100)（BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_currency", x => x.user_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_effect",
                columns: table => new
                {
                    instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "使い捨てGuid。1回の付与ごとに新規発行"),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照。効果保持者（Playerのみ。Enemyは出現の都度使い捨てのため永続効果を持つ意味がない）"),
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    affect_reason = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "付与要因（0: skill, 1: auto）"),
                    granter_entity_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "付与時点における付与者のentity_id（履歴的参照）。重複付与の一意性判定には使用しない（セッションごとの使い捨てGuidのため、判定に含めると常に「重複ではない」となり機能しない）"),
                    grant_source_key = table.Column<string>(type: "VARCHAR(64)", nullable: true, comment: "識別キー。skill付与時は skill_key。auto付与時は NULL")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    remaining_actions = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "残り有効行動数。保持者が1ターンに関与するごとに -1 し 0 で消滅。戦闘の境界では減衰も消滅もしない。NOT NULL: 永続スコープの効果は必ず有限でなければならない（終わりを保証するものが行動数しかないため）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_effect", x => x.instance_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_equipment",
                columns: table => new
                {
                    instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "使い捨てGuid。装備を入手する都度新規発行される"),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照。所有者"),
                    equip_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_equipment_master.equip_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_equipment", x => x.instance_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_equipment_slot",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照"),
                    weapon_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "chido_player_equipment.instance_id を参照。武器スロット"),
                    head_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "頭防具スロット"),
                    chest_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "胴防具スロット"),
                    legs_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "脚防具スロット"),
                    accessory1_instance_id = table.Column<byte[]>(type: "BINARY(16)", nullable: true, comment: "アクセサリスロット1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_equipment_slot", x => x.user_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_in_battle_session",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照。1プレイヤー1行という構造により「同時参加は1セッションまで」がテーブル構造から導かれる。行の不在＝非戦闘中"),
                    session_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_session.session_id を参照"),
                    entity_id = table.Column<byte[]>(type: "BINARY(16)", nullable: false, comment: "chido_battle_participant.entity_id を参照。(session_id, entity_id) によるPK直引きを可能にするための非正規化")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_in_battle_session", x => x.user_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_item",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照"),
                    item_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_item_master.item_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<uint>(type: "INT UNSIGNED", nullable: false, defaultValue: 0u, comment: "所持数")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_item", x => new { x.user_id, x.item_key });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_skill",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照"),
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_skill_master.skill_key を参照。通常攻撃（Attack）と防御（Defend）は習得管理の対象外であり行を持たない")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    learned_reason = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "習得理由（0: level, 1: item, 2: cheat）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_skill", x => new { x.user_id, x.skill_key });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_title",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照"),
                    title_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_title_master.title_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_title", x => new { x.user_id, x.title_key });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_player_title_display",
                columns: table => new
                {
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false, comment: "chido_player.user_id を参照"),
                    title_key = table.Column<string>(type: "VARCHAR(64)", nullable: true, comment: "chido_player_title.title_key を参照。NULL=称号を表示しない")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_player_title_display", x => x.user_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_skill_master",
                columns: table => new
                {
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "表示名")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "VARCHAR(500)", nullable: true, comment: "説明文")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    elements = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "スキル属性（ビット列）。ダメージ計算には一切使用しない、UI表示専用の\"見せかけ\"の値。ダメージ計算が参照するのは chido_skill_motion_attack_master.elements"),
                    require_tp = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "TP消費量（0-1000）。回復モーションを含むスキルでは 200 以上とする（運用制約）。166 以下では被反撃だけでTPが自給でき回復威力の実用帯が消滅する"),
                    learnable_level = table.Column<string>(type: "VARCHAR(100)", nullable: true, comment: "習得レベル。NULL=レベルアップでは習得不可。設計上は DECIMAL(33,0) UNSIGNED だが、BigInteger を DECIMAL 列へマップできないため VARCHAR(100)（BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    priority = table.Column<int>(type: "INT", nullable: false, defaultValue: 0, comment: "行動優先度。行動順は priority 降順 → Speed → Random。既定は 0（Attack・通常スキル）。Defend には正の値を与え、Speed に関わらず被弾前に構えを取れるようにする"),
                    special_process_key = table.Column<string>(type: "VARCHAR(64)", nullable: true, comment: "特殊処理呼び出し記号。NULL=標準の効果計算処理のみで完結")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_skill_master", x => x.skill_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_skill_motion_master",
                columns: table => new
                {
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_skill_master.skill_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    motion_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "再生順序。スキルはこの昇順にモーションを再生する"),
                    motion_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "モーション種別。サブタイプの判別子（0: 攻撃→10a, 1: 回復→10b, 2: 状態変化付与→10c, 3: 戦闘離脱→サブタイプなし, 4: 状態変化解除→10d）"),
                    target_rule = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "対象の解決規則（0: 自分自身, 1: 味方, 2: 敵）。常に単体固定。敵の味方対象モーションは chido_enemy_master.ally_target_rule で解決する"),
                    accuracy_rate = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "命中率（攻撃・回復）／成功率（状態変化付与・解除・戦闘離脱）。permyriad。4種すべてが使用する真の共通列であるため親に置く。Attack/Defend は 10000 固定"),
                    accuracy_gate_group = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: true, comment: "命中の依存グループ。NULL=単独で判定。同一 skill_key 内で同値の行が1グループを成し motion_index 最小の行を先頭とする。先頭が効果適用に到達しなければ同一グループの他メンバーは抽選せずスキップ。整合性検証はアプリ側の責務")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_skill_motion_master", x => new { x.skill_key, x.motion_index });
                    table.UniqueConstraint("uk_subtype", x => new { x.skill_key, x.motion_index, x.motion_type });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_title_master",
                columns: table => new
                {
                    title_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "可読キー")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "VARCHAR(100)", nullable: false, comment: "表示名")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    emoji = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "表示用絵文字。Unicode文字、またはDiscordカスタム絵文字の完成済みタグ文字列(<:name:id>)をそのまま格納")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    acquisition_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "入手条件種別（0: 特定アイテム獲得, 1: 特定敵撃破, 2: レベル到達, 3: 所持金額到達）。今後拡張予定"),
                    condition_key = table.Column<string>(type: "VARCHAR(64)", nullable: true, comment: "判定値(識別ID形式)。acquisition_type=0→item_key, 1→enemy_key を参照（参照先は acquisition_type により分岐）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    condition_value = table.Column<string>(type: "VARCHAR(100)", nullable: true, comment: "判定値(数値)。acquisition_type=2→レベル閾値、3→所持金額閾値。比較対象（exp由来のレベル、chido_player_currency.amount）と型を揃えている（いずれも VARCHAR(100)。BigIntegerToStringConverter 参照）")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_title_master", x => x.title_key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_skill_motion_attack_master",
                columns: table => new
                {
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    motion_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）"),
                    motion_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "0（攻撃）"),
                    attack_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "Physical/Magical。参照する攻撃力を選択する"),
                    power = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "威力。整数%（通常攻撃=100）。permyriad ではない点に注意。ダメージ = 攻撃力 × 威力 × 被防御係数(ATK÷(ATK+DEF))"),
                    elements = table.Column<uint>(type: "INT UNSIGNED", nullable: false, comment: "モーション属性（ビット列）。攻撃モーションのみが持つ。0（属性なし）が意味を持つ既定値（相性計算をスキップ＝全属性等倍）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_skill_motion_attack_master", x => new { x.skill_key, x.motion_index });
                    table.CheckConstraint("CK_chido_skill_motion_attack_master_motion_type", "motion_type = 0");
                    table.ForeignKey(
                        name: "FK_chido_skill_motion_attack_master_chido_skill_motion_master_s~",
                        columns: x => new { x.skill_key, x.motion_index, x.motion_type },
                        principalTable: "chido_skill_motion_master",
                        principalColumns: new[] { "skill_key", "motion_index", "motion_type" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_skill_motion_dispel_master",
                columns: table => new
                {
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    motion_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）"),
                    motion_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "4（状態変化解除）"),
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "解除対象。対象が保持する全スコープ（chido_battle_effect + chido_player_effect）から effect_key が一致する行をすべて削除する。granter_entity_id / grant_source_key / affect_reason は参照しない")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_skill_motion_dispel_master", x => new { x.skill_key, x.motion_index });
                    table.CheckConstraint("CK_chido_skill_motion_dispel_master_motion_type", "motion_type = 4");
                    table.ForeignKey(
                        name: "FK_chido_skill_motion_dispel_master_chido_effect_master_effect_~",
                        column: x => x.effect_key,
                        principalTable: "chido_effect_master",
                        principalColumn: "effect_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chido_skill_motion_dispel_master_chido_skill_motion_master_s~",
                        columns: x => new { x.skill_key, x.motion_index, x.motion_type },
                        principalTable: "chido_skill_motion_master",
                        principalColumns: new[] { "skill_key", "motion_index", "motion_type" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_skill_motion_effect_master",
                columns: table => new
                {
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    motion_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）"),
                    motion_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "2（状態変化付与）"),
                    effect_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "付与する状態変化。chido_effect_master.effect_key を参照")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    effect_rate = table.Column<int>(type: "INT", nullable: true, comment: "効果量。permyriad、符号あり（デバフの負値を許容）。付与先の fixed_rate が NULL の行に対してのみ必須。SlipDamage／DisableMove の効果量はそれぞれのマスタが持つため本列を使用しない"),
                    attack_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: true, comment: "Physical/Magical。付与する状態変化が SlipDamage 成分を持つ場合に継続ダメージの基準を決める。付与時に chido_effect_slip_damage_instance.attack_type へ複製される。SlipDamage 成分を持たない付与では NULL"),
                    duration_actions = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: true, comment: "持続。「残り有効行動数」であり時計ではない。remaining_actions の初期値として複製される。NULL=無期限、0 は取らない。付与先 effect の clear_on_battle_end=0 の場合は NOT NULL 必須（アプリ側の責務）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_skill_motion_effect_master", x => new { x.skill_key, x.motion_index });
                    table.CheckConstraint("CK_chido_skill_motion_effect_master_duration", "duration_actions IS NULL OR duration_actions >= 1");
                    table.CheckConstraint("CK_chido_skill_motion_effect_master_motion_type", "motion_type = 2");
                    table.ForeignKey(
                        name: "FK_chido_skill_motion_effect_master_chido_effect_master_effect_~",
                        column: x => x.effect_key,
                        principalTable: "chido_effect_master",
                        principalColumn: "effect_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chido_skill_motion_effect_master_chido_skill_motion_master_s~",
                        columns: x => new { x.skill_key, x.motion_index, x.motion_type },
                        principalTable: "chido_skill_motion_master",
                        principalColumns: new[] { "skill_key", "motion_index", "motion_type" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chido_skill_motion_heal_master",
                columns: table => new
                {
                    skill_key = table.Column<string>(type: "VARCHAR(64)", nullable: false, comment: "chido_skill_motion_master.skill_key を参照（判別子を含む複合FKの構成列）")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    motion_index = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "chido_skill_motion_master.motion_index を参照（判別子を含む複合FKの構成列）"),
                    motion_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "1（回復）"),
                    attack_type = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, comment: "Physical/Magical。参照する攻撃力を選択する"),
                    power = table.Column<ushort>(type: "SMALLINT UNSIGNED", nullable: false, comment: "威力。整数%。回復量 = 攻撃力 × 威力（対象の防御力は影響しない＝被防御係数1の攻撃）。同格では被防御係数が0.5になるため、通常攻撃(100%)と釣り合う回復は威力50%")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chido_skill_motion_heal_master", x => new { x.skill_key, x.motion_index });
                    table.CheckConstraint("CK_chido_skill_motion_heal_master_motion_type", "motion_type = 1");
                    table.ForeignKey(
                        name: "FK_chido_skill_motion_heal_master_chido_skill_motion_master_ski~",
                        columns: x => new { x.skill_key, x.motion_index, x.motion_type },
                        principalTable: "chido_skill_motion_master",
                        principalColumns: new[] { "skill_key", "motion_index", "motion_type" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_master_key",
                table: "chido_battle_enemy",
                column: "master_key");

            migrationBuilder.CreateIndex(
                name: "idx_enemy",
                table: "chido_battle_enemy_equipment",
                column: "enemy_id");

            migrationBuilder.CreateIndex(
                name: "idx_session_log",
                table: "chido_battle_log",
                columns: new[] { "session_id", "log_id" });

            migrationBuilder.CreateIndex(
                name: "uk_display_order",
                table: "chido_battle_participant",
                columns: new[] { "session_id", "entity_type", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uk_enemy_effect",
                table: "chido_enemy_effects_master",
                columns: new[] { "enemy_key", "effect_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_field_rarity",
                table: "chido_field_enemy_group_master",
                columns: new[] { "field_key", "rarity" });

            migrationBuilder.CreateIndex(
                name: "idx_user_equip",
                table: "chido_player_equipment",
                columns: new[] { "user_id", "equip_key" });

            migrationBuilder.CreateIndex(
                name: "idx_session",
                table: "chido_player_in_battle_session",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_chido_skill_motion_attack_master_skill_key_motion_index_moti~",
                table: "chido_skill_motion_attack_master",
                columns: new[] { "skill_key", "motion_index", "motion_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chido_skill_motion_dispel_master_effect_key",
                table: "chido_skill_motion_dispel_master",
                column: "effect_key");

            migrationBuilder.CreateIndex(
                name: "IX_chido_skill_motion_dispel_master_skill_key_motion_index_moti~",
                table: "chido_skill_motion_dispel_master",
                columns: new[] { "skill_key", "motion_index", "motion_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chido_skill_motion_effect_master_effect_key",
                table: "chido_skill_motion_effect_master",
                column: "effect_key");

            migrationBuilder.CreateIndex(
                name: "IX_chido_skill_motion_effect_master_skill_key_motion_index_moti~",
                table: "chido_skill_motion_effect_master",
                columns: new[] { "skill_key", "motion_index", "motion_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chido_skill_motion_heal_master_skill_key_motion_index_motion~",
                table: "chido_skill_motion_heal_master",
                columns: new[] { "skill_key", "motion_index", "motion_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chido_battle_effect");

            migrationBuilder.DropTable(
                name: "chido_battle_enemy");

            migrationBuilder.DropTable(
                name: "chido_battle_enemy_equipment");

            migrationBuilder.DropTable(
                name: "chido_battle_enemy_equipment_slot");

            migrationBuilder.DropTable(
                name: "chido_battle_log");

            migrationBuilder.DropTable(
                name: "chido_battle_participant");

            migrationBuilder.DropTable(
                name: "chido_battle_session");

            migrationBuilder.DropTable(
                name: "chido_battle_status");

            migrationBuilder.DropTable(
                name: "chido_channel_current_enemy");

            migrationBuilder.DropTable(
                name: "chido_channel_state");

            migrationBuilder.DropTable(
                name: "chido_effect_disable_move_master");

            migrationBuilder.DropTable(
                name: "chido_effect_element_grant_master");

            migrationBuilder.DropTable(
                name: "chido_effect_slip_damage_instance");

            migrationBuilder.DropTable(
                name: "chido_effect_slip_damage_master");

            migrationBuilder.DropTable(
                name: "chido_effect_status_modifier_instance");

            migrationBuilder.DropTable(
                name: "chido_effect_status_modifier_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_currency_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_effects_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_equipment_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_group_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_group_member_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_loots_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_master");

            migrationBuilder.DropTable(
                name: "chido_enemy_skills_master");

            migrationBuilder.DropTable(
                name: "chido_equipment_master");

            migrationBuilder.DropTable(
                name: "chido_field_enemy_group_master");

            migrationBuilder.DropTable(
                name: "chido_field_master");

            migrationBuilder.DropTable(
                name: "chido_field_rarity_rate_master");

            migrationBuilder.DropTable(
                name: "chido_field_transition_master");

            migrationBuilder.DropTable(
                name: "chido_item_master");

            migrationBuilder.DropTable(
                name: "chido_item_used_effect_master");

            migrationBuilder.DropTable(
                name: "chido_player");

            migrationBuilder.DropTable(
                name: "chido_player_currency");

            migrationBuilder.DropTable(
                name: "chido_player_effect");

            migrationBuilder.DropTable(
                name: "chido_player_equipment");

            migrationBuilder.DropTable(
                name: "chido_player_equipment_slot");

            migrationBuilder.DropTable(
                name: "chido_player_in_battle_session");

            migrationBuilder.DropTable(
                name: "chido_player_item");

            migrationBuilder.DropTable(
                name: "chido_player_skill");

            migrationBuilder.DropTable(
                name: "chido_player_title");

            migrationBuilder.DropTable(
                name: "chido_player_title_display");

            migrationBuilder.DropTable(
                name: "chido_skill_master");

            migrationBuilder.DropTable(
                name: "chido_skill_motion_attack_master");

            migrationBuilder.DropTable(
                name: "chido_skill_motion_dispel_master");

            migrationBuilder.DropTable(
                name: "chido_skill_motion_effect_master");

            migrationBuilder.DropTable(
                name: "chido_skill_motion_heal_master");

            migrationBuilder.DropTable(
                name: "chido_title_master");

            migrationBuilder.DropTable(
                name: "chido_effect_master");

            migrationBuilder.DropTable(
                name: "chido_skill_motion_master");
        }
    }
}
