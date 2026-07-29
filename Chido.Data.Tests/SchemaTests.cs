using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Chido.Data.Tests;

/// <summary>
/// スキーマの検証。実DBに接続せず、EF Core が確定させたモデルを読んで
/// chido-database-design.md の「確定スキーマ」章と突き合わせる。
///
/// Docker が使えない環境でも走り、CIに載せられることを重視している。
/// 実DBに対する MigrateAsync() の確認は、SELECT ... FOR UPDATE の検証で
/// どのみち実DBが要る Phase 6 で行う。
/// </summary>
public class SchemaTests
{
    // CHECK制約やコメントは実行時用に最適化されたモデルには載らないため、設計時モデルを読む
    private static readonly IModel Model = ChidoDbContextFactory
        .CreateDbContext("Server=localhost;Database=schema_test;User=u;Password=p;")
        .GetService<IDesignTimeModel>()
        .Model;

    /// <summary>設計ドキュメントの採番1〜45番＋スキルモーションのサブタイプ 10a〜10d。</summary>
    private static readonly string[] ExpectedTables =
    [
        "chido_player",                          //  1
        "chido_battle_status",                   //  2
        "chido_battle_session",                  //  3
        "chido_battle_participant",              //  4
        "chido_battle_log",                      //  5
        "chido_battle_enemy",                    //  6
        "chido_item_master",                     //  7
        "chido_player_item",                     //  8
        "chido_skill_master",                    //  9
        "chido_skill_motion_master",             // 10
        "chido_skill_motion_attack_master",      // 10a
        "chido_skill_motion_heal_master",        // 10b
        "chido_skill_motion_effect_master",      // 10c
        "chido_skill_motion_dispel_master",      // 10d
        "chido_enemy_master",                    // 11
        "chido_enemy_skills_master",             // 12
        "chido_enemy_loots_master",              // 13
        "chido_enemy_effects_master",            // 14
        "chido_effect_master",                   // 15
        "chido_effect_status_modifier_master",   // 16
        "chido_effect_slip_damage_master",       // 17
        "chido_effect_disable_move_master",      // 18
        "chido_battle_effect",                   // 19
        "chido_player_effect",                   // 20
        "chido_effect_status_modifier_instance", // 21
        "chido_effect_slip_damage_instance",     // 22
        "chido_player_skill",                    // 23
        "chido_item_used_effect_master",         // 24
        "chido_equipment_master",                // 25
        "chido_player_equipment",                // 26
        "chido_player_equipment_slot",           // 27
        "chido_enemy_equipment_master",          // 28
        "chido_battle_enemy_equipment",          // 29
        "chido_battle_enemy_equipment_slot",     // 30
        "chido_player_currency",                 // 31
        "chido_enemy_currency_master",           // 32
        "chido_title_master",                    // 33
        "chido_player_title",                    // 34
        "chido_player_title_display",            // 35
        "chido_player_in_battle_session",        // 36
        "chido_channel_state",                   // 37
        "chido_channel_current_enemy",           // 38
        "chido_field_master",                    // 39
        "chido_field_rarity_rate_master",        // 40
        "chido_field_transition_master",         // 41
        "chido_enemy_group_master",              // 42
        "chido_enemy_group_member_master",       // 43
        "chido_field_enemy_group_master",        // 44
        "chido_effect_element_grant_master",     // 45
    ];

    private static IEntityType Table(string name) =>
        Model.GetEntityTypes().Single(t => t.GetTableName() == name);

    private static IProperty Column(string table, string column) =>
        Table(table).GetProperties().Single(p => p.GetColumnName() == column);

    [Fact]
    public void 設計ドキュメントの全テーブルが定義されている()
    {
        var actual = Model.GetEntityTypes().Select(t => t.GetTableName()!).ToHashSet();

        var missing = ExpectedTables.Where(t => !actual.Contains(t)).ToList();
        var unexpected = actual.Where(t => !ExpectedTables.Contains(t)).ToList();

        Assert.Empty(missing);
        Assert.Empty(unexpected);
        Assert.Equal(49, ExpectedTables.Length);
    }

    [Fact]
    public void 全列が明示的なスネークケース名を持つ()
    {
        // 命名をEF Coreの既定（プロパティ名そのまま）に委ねると、設計ドキュメントのDDLと乖離する
        foreach (var entity in Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName();
                Assert.True(column == column?.ToLowerInvariant(),
                    $"{entity.GetTableName()}.{column} が小文字スネークケースでない");
            }
        }
    }

    [Fact]
    public void 全列にコメントが付いている()
    {
        // HasComment は設計ドキュメントのDDLコメントをスキーマへ持ち込むための資産であり、
        // スキーマ単体から意図を追えるようにしている。付け忘れを機械的に検出する。
        var missing = new List<string>();

        foreach (var entity in Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (string.IsNullOrWhiteSpace(property.GetComment()))
                    missing.Add($"{entity.GetTableName()}.{property.GetColumnName()}");
            }
        }

        Assert.Empty(missing);
    }

    // --- 主キー ---

    [Theory]
    [InlineData("chido_player", "user_id")]
    [InlineData("chido_battle_status", "user_id")]
    [InlineData("chido_battle_session", "session_id")]
    [InlineData("chido_battle_participant", "session_id,entity_id")]
    [InlineData("chido_battle_log", "log_id")]
    [InlineData("chido_battle_enemy", "enemy_id")]
    [InlineData("chido_player_item", "user_id,item_key")]
    [InlineData("chido_skill_motion_master", "skill_key,motion_index")]
    [InlineData("chido_skill_motion_attack_master", "skill_key,motion_index")]
    [InlineData("chido_enemy_effects_master", "enemy_key,enemy_effect_index")]
    [InlineData("chido_effect_status_modifier_master", "effect_key,target_status")]
    [InlineData("chido_effect_status_modifier_instance", "instance_id,target_status")]
    [InlineData("chido_player_in_battle_session", "user_id")]
    [InlineData("chido_channel_state", "channel_id")]
    [InlineData("chido_channel_current_enemy", "channel_id,spawn_index")]
    [InlineData("chido_field_rarity_rate_master", "field_key,rarity")]
    [InlineData("chido_field_transition_master", "field_key,next_field_key")]
    [InlineData("chido_enemy_group_member_master", "group_key,member_index")]
    [InlineData("chido_field_enemy_group_master", "field_key,group_key")]
    public void 主キーが設計と一致する(string table, string expectedColumns)
    {
        var actual = string.Join(",", Table(table).FindPrimaryKey()!.Properties.Select(p => p.GetColumnName()));
        Assert.Equal(expectedColumns, actual);
    }

    [Fact]
    public void 単一行の構造が主キーから導かれている()
    {
        // 「1つ以下」という制約をテーブル構造そのものから導く方針の適用箇所（設計ドキュメント 横断的な方針）。
        // 複合PKにすると一意性が失われ、参加判定も全走査になる
        foreach (var table in new[]
                 {
                     "chido_player_in_battle_session", "chido_player_title_display",
                     "chido_player_equipment_slot", "chido_channel_state",
                 })
        {
            var pk = Table(table).FindPrimaryKey()!;
            Assert.Single(pk.Properties);
        }
    }

    // --- 一意制約・インデックス ---

    [Fact]
    public void 表示順の一意制約が定義されている()
    {
        // 組の一括INSERTで joined_at がミリ秒精度で同値になり、ターゲット自動再選定の
        // 「先頭の敵」が一意に定まらなかったことへの対応（設計ドキュメント 4番）。
        // MAX+1 採番の最後の砦としても機能する
        var index = Table("chido_battle_participant").GetIndexes()
            .Single(i => i.GetDatabaseName() == "uk_display_order");

        Assert.True(index.IsUnique);
        Assert.Equal(["session_id", "entity_type", "display_order"],
            index.Properties.Select(p => p.GetColumnName()));
    }

    [Fact]
    public void 敵の初期付与状態変化の一意制約が定義されている()
    {
        // 同一の敵に同じ effect_key を2行定義できると、戦闘開始時に重複判定キーが完全一致し
        // 2行目以降が実行時に黙って捨てられる。入力時に弾く（設計ドキュメント 14番）
        var index = Table("chido_enemy_effects_master").GetIndexes()
            .Single(i => i.GetDatabaseName() == "uk_enemy_effect");

        Assert.True(index.IsUnique);
        Assert.Equal(["enemy_key", "effect_key"], index.Properties.Select(p => p.GetColumnName()));
    }

    [Theory]
    [InlineData("chido_battle_log", "idx_session_log")]
    [InlineData("chido_player_equipment", "idx_user_equip")]
    [InlineData("chido_battle_enemy_equipment", "idx_enemy")]
    [InlineData("chido_player_in_battle_session", "idx_session")]
    [InlineData("chido_field_enemy_group_master", "idx_field_rarity")]
    public void 設計ドキュメントのインデックスが定義されている(string table, string indexName)
    {
        Assert.Contains(Table(table).GetIndexes(), i => i.GetDatabaseName() == indexName);
    }

    // --- CHECK制約 ---

    [Fact]
    public void 参加者の種別ごとのCHECK制約が定義されている()
    {
        var checks = Table("chido_battle_participant").GetCheckConstraints().ToList();
        var check = Assert.Single(checks);
        Assert.Contains("entity_type = 0", check.Sql);
        Assert.Contains("entity_type = 1", check.Sql);
    }

    [Theory]
    [InlineData("chido_skill_motion_attack_master", "motion_type = 0")]
    [InlineData("chido_skill_motion_heal_master", "motion_type = 1")]
    [InlineData("chido_skill_motion_effect_master", "motion_type = 2")]
    [InlineData("chido_skill_motion_dispel_master", "motion_type = 4")]
    public void サブタイプの判別子がCHECK制約で固定されている(string table, string expectedSql)
    {
        Assert.Contains(Table(table).GetCheckConstraints(), c => c.Sql.Contains(expectedSql));
    }

    [Theory]
    [InlineData("chido_skill_motion_effect_master")]
    [InlineData("chido_enemy_effects_master")]
    public void 持続の下限がCHECK制約で守られている(string table)
    {
        // duration_actions は NULL（無期限）を取りうるが 0 は取らない
        Assert.Contains(Table(table).GetCheckConstraints(),
            c => c.Sql.Contains("duration_actions IS NULL OR duration_actions >= 1"));
    }

    // --- サブタイプの複合外部キー ---

    [Theory]
    [InlineData("chido_skill_motion_attack_master")]
    [InlineData("chido_skill_motion_heal_master")]
    [InlineData("chido_skill_motion_effect_master")]
    [InlineData("chido_skill_motion_dispel_master")]
    public void サブタイプは判別子を含む複合FKで親を参照する(string table)
    {
        // motion_type を含めることで、攻撃行が回復として登録される誤りをDBが弾ける（設計ドキュメント 10番）
        var fk = Table(table).GetForeignKeys()
            .Single(f => f.PrincipalEntityType.GetTableName() == "chido_skill_motion_master");

        Assert.Equal(["skill_key", "motion_index", "motion_type"],
            fk.Properties.Select(p => p.GetColumnName()));
        Assert.Equal(["skill_key", "motion_index", "motion_type"],
            fk.PrincipalKey.Properties.Select(p => p.GetColumnName()));
    }

    [Fact]
    public void 親側にサブタイプ参照用の代替キーがある()
    {
        var ak = Table("chido_skill_motion_master").GetKeys()
            .Single(k => !k.IsPrimaryKey());

        Assert.Equal(["skill_key", "motion_index", "motion_type"],
            ak.Properties.Select(p => p.GetColumnName()));
    }

    [Fact]
    public void 外部キーはスキルモーション周辺に限られる()
    {
        // 設計ドキュメント全体はコメントベースの参照で統一されており、
        // 明示的なFOREIGN KEYを持つのはスキルモーションのサブタイプ周辺のみ。
        // EF Coreの暗黙のFK自動生成が紛れ込んでいないことを確認する
        var tablesWithFk = Model.GetEntityTypes()
            .Where(t => t.GetForeignKeys().Any())
            .Select(t => t.GetTableName()!)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal([
            "chido_skill_motion_attack_master",
            "chido_skill_motion_dispel_master",
            "chido_skill_motion_effect_master",
            "chido_skill_motion_heal_master",
        ], tablesWithFk);
    }

    [Fact]
    public void 状態変化のインスタンス側にはFKを張らない()
    {
        // 21・22番は chido_battle_effect と chido_player_effect の両方の instance_id を受け入れる
        // 共有テーブルであり、MySQLのFKは単一テーブルしか参照できないため張れない
        Assert.Empty(Table("chido_effect_status_modifier_instance").GetForeignKeys());
        Assert.Empty(Table("chido_effect_slip_damage_instance").GetForeignKeys());
    }

    // --- 列の型 ---

    [Theory]
    [InlineData("chido_battle_participant", "session_id")]
    [InlineData("chido_battle_participant", "entity_id")]
    [InlineData("chido_battle_participant", "enemy_id")]
    [InlineData("chido_battle_participant", "current_target_id")]
    [InlineData("chido_battle_effect", "instance_id")]
    [InlineData("chido_battle_effect", "granter_entity_id")]
    [InlineData("chido_player_equipment", "instance_id")]
    [InlineData("chido_channel_state", "current_session_id")]
    public void 使い捨てIDはBINARY16である(string table, string column)
    {
        Assert.Equal("BINARY(16)", Column(table, column).GetColumnType());
    }

    [Theory]
    [InlineData("chido_battle_status", "exp")]
    [InlineData("chido_battle_participant", "current_hp")]
    [InlineData("chido_battle_participant", "total_damage_dealt")]
    [InlineData("chido_battle_enemy", "level")]
    [InlineData("chido_player_currency", "amount")]
    [InlineData("chido_enemy_currency_master", "drop_amount")]
    [InlineData("chido_equipment_master", "progression_value")]
    [InlineData("chido_channel_state", "cumulative_enemy_level")]
    [InlineData("chido_effect_slip_damage_instance", "status_attack_value")]
    [InlineData("chido_skill_master", "learnable_level")]
    [InlineData("chido_title_master", "condition_value")]
    public void BigIntegerの列はすべてVARCHAR100である(string table, string column)
    {
        // DECIMAL は使えない。MySqlConnector が DECIMAL 列を decimal へパースするため、
        // System.Decimal の 28〜29桁を超える値は読み出し時に落ちる（EF ではなくコネクタ側の制約）。
        // 詳細は BigIntegerToStringConverter を参照
        Assert.Equal("VARCHAR(100)", Column(table, column).GetColumnType());
    }

    [Theory]
    [InlineData("chido_battle_status", "exp", "exp_len")]
    [InlineData("chido_player_currency", "amount", "amount_len")]
    public void ランキング対象の列は桁数の生成列を持つ(string table, string column, string lengthColumn)
    {
        // 10進整数文字列の素の ORDER BY は辞書順（"9" > "10"）になる。
        // 非負の正準10進文字列では (桁数, 辞書順) が数値順に一致するため、
        // 桁数をストアド生成列に持たせて第1ソートキーに使う（Chido.Data.Queries.RankingQueries）
        var length = Column(table, lengthColumn);

        Assert.Equal("TINYINT UNSIGNED", length.GetColumnType());
        Assert.Equal($"CHAR_LENGTH(`{column}`)", length.GetComputedColumnSql());
        Assert.True(length.GetIsStored());
    }

    [Theory]
    [InlineData("chido_battle_status", "exp", "exp_len")]
    [InlineData("chido_player_currency", "amount", "amount_len")]
    public void ランキング対象の列は照合順序がascii_binである(string table, string column, string _)
    {
        // 照合順序に依存せず必ずバイト順で比較させる。インデックスも1バイト/文字になる
        Assert.Equal("ascii_bin", Column(table, column).GetCollation());
    }

    [Theory]
    [InlineData("chido_battle_status", "idx_exp_rank", "exp_len", "exp")]
    [InlineData("chido_player_currency", "idx_amount_rank", "amount_len", "amount")]
    public void ランキング用の複合インデックスが定義されている(
        string table, string indexName, string first, string second)
    {
        // 昇順のまま張る。MySQL 8 は ORDER BY exp_len DESC, exp DESC のような全反転を
        // 昇順インデックスの逆走査（Backward index scan）で処理でき、filesort が出ない
        var index = Table(table).GetIndexes().Single(i => i.GetDatabaseName() == indexName);

        Assert.Equal([first, second], index.Properties.Select(p => p.GetColumnName()));
    }

    [Theory]
    [InlineData("chido_battle_participant", "entity_type")]
    [InlineData("chido_battle_participant", "status")]
    [InlineData("chido_battle_session", "end_reason")]
    [InlineData("chido_battle_log", "action_type")]
    [InlineData("chido_skill_motion_master", "motion_type")]
    [InlineData("chido_skill_motion_master", "target_rule")]
    [InlineData("chido_enemy_master", "rarity")]
    [InlineData("chido_enemy_master", "action_pattern_type")]
    [InlineData("chido_enemy_master", "ally_target_rule")]
    [InlineData("chido_effect_status_modifier_master", "target_status")]
    [InlineData("chido_battle_effect", "affect_reason")]
    [InlineData("chido_player_skill", "learned_reason")]
    [InlineData("chido_item_master", "item_type")]
    [InlineData("chido_item_used_effect_master", "item_usage_type")]
    [InlineData("chido_title_master", "acquisition_type")]
    public void 列挙値はTINYINT_UNSIGNEDである(string table, string column)
    {
        Assert.Equal("TINYINT UNSIGNED", Column(table, column).GetColumnType());
    }

    [Theory]
    [InlineData("chido_skill_master", "elements")]
    [InlineData("chido_skill_motion_attack_master", "elements")]
    [InlineData("chido_enemy_master", "elements")]
    [InlineData("chido_equipment_master", "elements")]
    [InlineData("chido_equipment_master", "equip_parts")]
    [InlineData("chido_effect_master", "effect_types")]
    [InlineData("chido_effect_slip_damage_master", "elements")]
    [InlineData("chido_effect_element_grant_master", "elements")]
    public void ビット列はINT_UNSIGNEDである(string table, string column)
    {
        Assert.Equal("INT UNSIGNED", Column(table, column).GetColumnType());
    }

    [Theory]
    // 常に非負で結果値として完結する割合値 → SMALLINT UNSIGNED（0〜10000）
    [InlineData("chido_skill_motion_master", "accuracy_rate", "SMALLINT UNSIGNED")]
    [InlineData("chido_enemy_loots_master", "drop_rate", "SMALLINT UNSIGNED")]
    [InlineData("chido_enemy_effects_master", "grant_rate", "SMALLINT UNSIGNED")]
    [InlineData("chido_enemy_equipment_master", "equip_rate", "SMALLINT UNSIGNED")]
    [InlineData("chido_effect_disable_move_master", "disable_rate", "SMALLINT UNSIGNED")]
    [InlineData("chido_field_rarity_rate_master", "rarity_rate", "SMALLINT UNSIGNED")]
    // 6.55倍を超えうる倍率 → INT UNSIGNED
    [InlineData("chido_enemy_master", "strength_rate", "INT UNSIGNED")]
    [InlineData("chido_enemy_master", "exp_rate", "INT UNSIGNED")]
    // バフ／デバフ双方向を許容する符号あり → INT
    [InlineData("chido_effect_status_modifier_master", "fixed_rate", "INT")]
    [InlineData("chido_effect_status_modifier_instance", "rate", "INT")]
    [InlineData("chido_skill_motion_effect_master", "effect_rate", "INT")]
    [InlineData("chido_enemy_effects_master", "effect_rate", "INT")]
    [InlineData("chido_equipment_master", "hp_rate", "INT")]
    [InlineData("chido_equipment_master", "luck_bonus_rate", "INT")]
    public void 割合値は用途に応じた型を持つ(string table, string column, string expected)
    {
        Assert.Equal(expected, Column(table, column).GetColumnType());
    }

    [Theory]
    // permyriad ではないため Ratio の対象外（設計ドキュメント 割合値のスケールと命名規約）
    [InlineData("chido_enemy_master", "hp_shape", "SMALLINT UNSIGNED")]
    [InlineData("chido_skill_motion_attack_master", "power", "SMALLINT UNSIGNED")]
    [InlineData("chido_skill_motion_heal_master", "power", "SMALLINT UNSIGNED")]
    [InlineData("chido_effect_slip_damage_master", "power", "SMALLINT UNSIGNED")]
    [InlineData("chido_enemy_skills_master", "weight", "TINYINT UNSIGNED")]
    [InlineData("chido_equipment_master", "speed_bonus", "INT")]
    public void permyriad以外の数値は変換対象外の型を持つ(string table, string column, string expected)
    {
        Assert.Equal(expected, Column(table, column).GetColumnType());
    }

    // --- NULL可否 ---

    [Fact]
    public void 永続スコープの残り有効行動数はNOT_NULLである()
    {
        // 永続スコープの効果は必ず有限でなければならない（終わりを保証するものが行動数しかないため）。
        // NULL を許すと「真に永久」な効果が表現可能になり、加算合成される永続デバフが単調増加する
        Assert.False(Column("chido_player_effect", "remaining_actions").IsNullable);
    }

    [Fact]
    public void 戦闘内スコープの残り有効行動数はNULL許容である()
    {
        // こちらは戦闘終了という終わりが別途保証されているため、NULL（無期限）を取れる
        Assert.True(Column("chido_battle_effect", "remaining_actions").IsNullable);
    }

    [Theory]
    [InlineData("chido_battle_session", "ended_at")]
    [InlineData("chido_battle_session", "end_reason")]
    [InlineData("chido_battle_participant", "current_target_id")]
    [InlineData("chido_skill_master", "learnable_level")]
    [InlineData("chido_skill_motion_master", "accuracy_gate_group")]
    [InlineData("chido_skill_motion_effect_master", "effect_rate")]
    [InlineData("chido_skill_motion_effect_master", "attack_type")]
    [InlineData("chido_skill_motion_effect_master", "duration_actions")]
    [InlineData("chido_effect_status_modifier_master", "fixed_rate")]
    [InlineData("chido_channel_state", "current_session_id")]
    [InlineData("chido_player_title_display", "title_key")]
    public void NULLに意味を持つ列がNULL許容である(string table, string column)
    {
        Assert.True(Column(table, column).IsNullable);
    }

    [Fact]
    public void 廃止された列が残っていない()
    {
        // 2番の current_hp は削除された（現在HPの真値は chido_battle_participant.current_hp のみ）
        Assert.DoesNotContain(Table("chido_battle_status").GetProperties(),
            p => p.GetColumnName() == "current_hp");

        // 3番の last_action_at は Timeout 廃止により用途が消失したため削除した
        Assert.DoesNotContain(Table("chido_battle_session").GetProperties(),
            p => p.GetColumnName() == "last_action_at");
    }

    [Fact]
    public void 一時的な属性付与にインスタンス側テーブルは存在しない()
    {
        // 付与元（10c・14番）のどちらにも elements 列が無く、付与される属性は effect_key ごとに
        // マスタ側で固定されているため、インスタンスごとに変わる値が存在しない
        Assert.DoesNotContain(Model.GetEntityTypes(),
            t => t.GetTableName() == "chido_effect_element_grant_instance");

        // 行動不能も確率のみで完結するため同様
        Assert.DoesNotContain(Model.GetEntityTypes(),
            t => t.GetTableName() == "chido_effect_disable_move_instance");
    }
}
