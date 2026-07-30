# Chido データベース設計

対象: Discord製ターン制RPG Bot「Chido」のMySQLスキーマ。
本ドキュメントはClaude Code等のAIエージェントへの引き渡しを想定し、各カラムに設計意図をSQLコメントとして明記している。

`chido-battle-system-design.md`（戦闘システム設計ドキュメント）との整合を取った改訂版。両ドキュメントで記述が食い違う場合、**スキーマ定義については本ドキュメントを、戦闘ロジック・仕様については戦闘システムドキュメントを正**とする。

## 開発環境

- 開発OS: Windows 11
- 言語/フレームワーク: C# / .NET 8
- データベース: MySQL 8.4 LTS（8.0 系は 2026-04-30 にEOL）
- 開発ツール: Claude Code
- ホスティングサーバー: Ubuntu 24.04 LTS
- リポジトリ: https://github.com/Komini532/Chido/

---

## 本改訂の要約

戦闘システム設計ドキュメントとの整合を取るにあたり、以下を反映した。

**新規テーブル（10件）**

- `chido_player_in_battle_session` — 参加中セッションの保持。1プレイヤー1セッション制約の担保
- `chido_channel_state` — チャンネル単位の永続状態（現在フィールド・累積敵レベル・現在セッション）
- `chido_channel_current_enemy` — 現在出現中の敵の集合
- `chido_field_master` / `chido_field_rarity_rate_master` / `chido_field_transition_master` — フィールドシステム
- `chido_enemy_group_master` / `chido_enemy_group_member_master` / `chido_field_enemy_group_master` — 敵の「組」による出現抽選
- `chido_effect_element_grant_master` — 一時的な属性付与

**主要な既存テーブルの変更**

- `chido_battle_status.current_hp` を削除（現在HPの真値は `chido_battle_participant.current_hp` 一箇所のみ）
- `chido_battle_participant` に `status`（ParticipantStatus）と `current_tp` を追加
- `chido_battle_session.end_reason` を4値に（`PlayerVictory` / `PlayerEscaped` / `EnemyEscaped` / `ChannelMissing`）
- 割合値のスケールを permyriad（10000 = 100%）に統一し、命名規約（`_shape` / `_rate` / `_bonus_rate` / `_bonus` / `power` / `weight`）を確立
- `chido_enemy_master.*_rate` → `*_shape` にリネーム、`elements` / `strength_rate` / `exp_rate` を追加
- `chido_skill_motion_master.amount` を `power` / `effect_rate` に分割

**戦闘システムドキュメント 8.3 の記述の訂正**

同ドキュメントは `chido_player_skill`・装備関連・通貨/称号・アイテム効果を「未着手／未設計」としているが、いずれも本ドキュメントで確定済みである（23〜35番）。8.3 は戦闘システムドキュメント側の修正対象であり、DB側の追記事項ではない。

---

## 第2次改訂の要約（未確定事項の解消）

戦闘システム設計ドキュメントの「必須」未確定事項6件をすべて確定させ、その帰結として派生した決定を反映した。**必須の未確定事項は両ドキュメントとも0件になった。**

**新規テーブル（3件）— スキルモーションマスタのサブタイプ化**

- `chido_skill_motion_hp_master` — 現在HPへの干渉（攻撃・回復）※第3次改訂で `chido_skill_motion_attack_master`（攻撃）と `chido_skill_motion_heal_master`（回復）に分割
- `chido_skill_motion_effect_master` — 状態変化付与
- `chido_skill_motion_dispel_master` — 状態変化解除

`chido_skill_motion_master` を親（スーパータイプ）とするクラステーブル継承。自然キー `(skill_key, motion_index)` を親子で共有し、サロゲートキーは導入しない。判別子（`motion_type`）を含む複合FKにより、サブタイプの誤接続をDBレベルで防ぐ。`chido_effect_master` + サブテーブル群（16・17・18・45番）が既に採用している様式を、モーションにも適用したもの。

**主要な既存テーブルの変更**

- `chido_battle_participant` に `display_order` を追加。`joined_at` から表示順の責務を剥奪（必須 E-6 の解消）
- `chido_skill_master.range_type` を削除し、`chido_skill_motion_master.target_rule` として移設。**スキル単位では「自分を強化 → 敵を攻撃」のような複数対象のモーション列が表現できなかった**という既存の欠陥の修正を兼ねる
- `chido_skill_motion_master` に `duration_actions` を追加し、上記3テーブルへ分割（必須 G-2 の解消）
- `chido_enemy_effects_master` に `duration_actions` と `UNIQUE KEY (enemy_key, effect_key)` を追加
- `chido_battle_effect.remaining_turns` → `remaining_actions`（`SMALLINT UNSIGNED`）に改称・型変更
- `chido_player_effect` に `remaining_actions`（NOT NULL）を追加。**永続スコープの状態変化は「戦闘を跨ぎつつ有限」である**ことが確定（I-1 の解消）
- `chido_battle_status.exp` の初期値を `1` と明記（`level = √exp` のため `0` では全ステータスが0になる）
- `motion_type` に `4: 状態変化解除` を追加

**削除された概念**

- **防御貫通率**: ダメージ計算パイプラインの1段目に記述があったが、データ供給源がスキーマ上に存在せず、実装コードの構造が写経されたものである可能性が高い。かつ `StatusModifier` の DEF デバフで表現可能な冗長機能であるため廃止（戦闘システム 5.1参照）
- **`chido_element_affinity_master`**: 属性相性表はC#定数として保持することが確定したため、新設しない（戦闘システム 5.3参照）

**用語の変更**

- 「残りターン数」→「**残り有効行動数**」。減衰の契機は時間の経過ではなく保持者がターンに関与したことであり、「ターン」という語が持つ「戦闘内の時計」という含意が、永続スコープへの適用可否の判断を誤らせていたため
- 「影響範囲種別（`range_type`）」→「**対象の解決規則（`target_rule`）**」。影響範囲は常に単体固定であり、実体は対象の解決規則であるため

---

## 第3次改訂の要約（推奨事項の解消・スキルマスタ着手前の確定）

戦闘システム設計ドキュメントの「必須」7件（A-2/A-6/A-7/A-8/A-9/B-9/B-11）と、マスタデータ作成前に決めるべき「推奨〔優先度：高〕」3件（11.2-5/11.2-6/11.2-11）を確定させ、その帰結を反映した。スキーマに波及するのは以下3点で、いずれもDB未構築のため定義の書き直しのみで完結する。

**スキーマ変更（3件）**

- `chido_skill_motion_master` の親から `elements` を削除し、`accuracy_gate_group SMALLINT UNSIGNED NULL` を追加。共通列は `target_rule` / `accuracy_rate` / `accuracy_gate_group` の3つになった
- 旧 `chido_skill_motion_hp_master`（攻撃・回復）を、`chido_skill_motion_attack_master`（10a・攻撃、`elements` を保持）と `chido_skill_motion_heal_master`（10b・回復、属性なし）に分割。子は 10a=攻撃 / 10b=回復 / 10c=付与 / 10d=解除 となり、`motion_type` と1対1に対応する
- `chido_enemy_master` に `ally_target_rule TINYINT UNSIGNED NOT NULL DEFAULT 0` を追加（敵の味方対象モーションの対象選択規則。族ごとに番号を予約）

**コメント追記のみ（スキーマ変更なし）**

- `chido_battle_participant.current_target_id`：初回既定・再選定を区別しない単一の導出関数と書き戻し方式（戦闘システム 3.3）
- `chido_effect_disable_move_master.disable_rate`：行動しようとするたびに引く確率（戦闘システム 5.4 の A-7）
- `chido_field_transition_master`：自己参照行が「意図的な行き止まり」を表し、行が無い場合は縮退であること（B-11）
- `chido_field_enemy_group_master`：草原 `Common` の行が必須であること（B-10 のフォールバック先）

**属性（`elements`）の位置づけ変更**

属性はモーションのうち攻撃モーションのみが持つことが確定した。回復・状態変化付与・解除・戦闘離脱はモーション属性を持たない。かつて親に置く根拠としていた「モーション属性に反応する効果」（戦闘システム 11.4-4）は、「モーションに反応する効果」へ方針変更され、属性への反応は不要となった。

---

## 第4次改訂の要約（DECIMAL の撤回と巨大数値のソート）

`DECIMAL(65,0)` が.NETから扱えないことが判明したため撤回し、巨大数値のソートを別の方式で成立させた。実装（`Chido.Data`）は既に `VARCHAR(100)` で全列を運用しており、本改訂はドキュメントを実態に合わせるとともに、失われていた「SQL側での数値順ソート」を回復させるもの。

**`DECIMAL(65,0)` の撤回**

原因はEF CoreやPomeloではなく、その下のMySqlConnectorにある。`DECIMAL` 列は `System.Decimal`（上限28〜29桁）へパースされるため、それを超える値は読み出し時に例外になる。`GetString` も生バイトを読み直さないため文字列で逃がすこともできない。**MySqlConnectorを使う限りPomelo・Dapper・生のADO.NETのいずれでも同じであり、ORMの乗り換えでは解決しない。** 加えて65桁という上限自体がインフレ型のゲーム性に合わない。

**桁数の生成列によるソート（新方式）**

非負の正準10進文字列では `(桁数, 辞書順)` が数値順に一致する。この性質を使い、桁数のストアド生成列との複合インデックスで数値順のソートを得る。詳細は「巨大数値の格納」を参照。

**スキーマ変更（2テーブル）**

- `chido_battle_status` に `exp_len TINYINT UNSIGNED AS (CHAR_LENGTH(exp)) STORED` と `INDEX idx_exp_rank (exp_len, exp)` を追加。`exp` を `ascii_bin` に変更
- `chido_player_currency` に `amount_len` と `INDEX idx_amount_rank (amount_len, amount)` を追加。`amount` を `ascii_bin` に変更

**型定義の修正（8列、実装済みの内容への追随）**

`chido_battle_status.exp` / `chido_battle_enemy.level` / `chido_skill_master.learnable_level` / `chido_equipment_master.progression_value` / `chido_player_currency.amount` / `chido_enemy_currency_master.drop_amount` / `chido_title_master.condition_value` / `chido_channel_state.cumulative_enemy_level` を `VARCHAR(100)` に修正。`learnable_level` の「33桁」という桁数見積りの根拠（`exp` が `DECIMAL(65,0)` であること）は失効したため削除した。

**帰結**

- `chido_player_currency` の加減算を `UPDATE ... SET amount = amount ± X` で行う旨の記述を撤回した。読み出して `BigInteger` で計算し書き戻す
- 100桁を超える値は書き込み時に例外になる（従来は静かに切り詰められうる状態だった）
- 実DBに対する `MigrateAsync()` の適用をMySQL 8.0で初めて確認した（全49テーブル作成・生成列・逆走査インデックスの動作を含む）

### ID体系の使い分け

| 種別 | 型 | 例 | 性質 |
|---|---|---|---|
| 永続ID（実体が1つ、繰り返し参照される） | `BIGINT UNSIGNED` | `chido_player.user_id`, `chido_channel_state.channel_id` | Discordスノーフレークをそのまま利用。64bit符号なしに収まる。 |
| 使い捨てID（発生の都度新規発行、再利用されない） | `BINARY(16)` (Guid) | `chido_battle_session.session_id`, `chido_battle_participant.entity_id`, `chido_battle_enemy.enemy_id`, `chido_battle_effect.instance_id`, `chido_player_effect.instance_id`, `chido_player_equipment.instance_id`, `chido_battle_enemy_equipment.instance_id` | 戦闘・参加者・敵の出現インスタンス、状態変化の付与インスタンス、装備インスタンスなど、発生の都度新規発行される実体を表す。 |
| 可読キー（人力で編集・参照するコンテンツ定義） | `VARCHAR(64)` | `chido_item_master.item_key`, `chido_skill_master.skill_key`, `chido_enemy_master.enemy_key`, `chido_effect_master.effect_key`, `chido_equipment_master.equip_key`, `chido_enemy_group_master.group_key`, `chido_field_master.field_key` | マスタデータはGUIDより文字列キーの方がバランス調整やログ確認、AIへの指示出しの際に扱いやすい。 |

### 巨大数値の格納（本改訂で確定）

ステータス類は`BigInteger`（C#）で扱う前提の巨大数値になりうる。**これらは例外なく`VARCHAR(100)`に10進整数文字列として格納する。**

**`DECIMAL(65,0)`は採用しない。** かつて本節は「SQL側での比較・ソートが必要な値は`DECIMAL(65,0) UNSIGNED`」と定めていたが、この型は.NETから扱えないことが判明したため撤回する。理由はEF CoreやPomeloの層ではなく、その下のコネクタの層にある。

- MySqlConnectorは`DECIMAL`列を`Utf8Parser.TryParse(data, out decimal)`で読む。`System.Decimal`の上限は28〜29桁であり、それを超える値は**読み出し時に例外**になる。
- `GetString(ordinal)`も生バイトを読み直さず`(string)GetValue(ordinal)`とキャストするだけなので、文字列として逃がすこともできない。
- SELECTごとに`CAST(col AS CHAR)`を書く以外の回避手段がなく、これはMySqlConnectorを使う限りPomelo・Dapper・生のADO.NETのいずれでも同じである。ORMの乗り換えでは解決しない。
- そもそも`DECIMAL`は精度65桁が絶対上限であり、乗算的にインフレする経験値やダメージ量にはこの上限自体が適さない。

**SQL側での数値順のソートは、桁数のストアド生成列との複合インデックスで実現する。** 非負の正準10進文字列（`BigInteger.ToString()`が保証する。先頭に余分な`0`が付かない）では、**`(桁数, 辞書順)`が数値順と完全に一致する**。

```sql
exp     VARCHAR(100)     CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
exp_len TINYINT UNSIGNED AS (CHAR_LENGTH(exp)) STORED,
INDEX idx_exp_rank (exp_len, exp)

-- ORDER BY exp_len DESC, exp DESC  →  数値降順
```

インデックスは昇順のまま張る。MySQL 8は全反転（`DESC, DESC`）を昇順インデックスの逆走査で処理するため、`filesort`は発生しない（実測: `Backward index scan; Using index`）。照合順序を`ascii_bin`にするのは、照合順序に依存せず必ずバイト順で比較させるためと、インデックスを1バイト/文字に抑えるため。

対象は**実際にSQL側のソートが必要な2列のみ**とする。

| 列 | 生成列 | インデックス |
|---|---|---|
| `chido_battle_status.exp` | `exp_len` | `idx_exp_rank (exp_len, exp)` |
| `chido_player_currency.amount` | `amount_len` | `idx_amount_rank (amount_len, amount)` |

残りの巨大数値列（`chido_battle_enemy.level`、`chido_channel_state.cumulative_enemy_level`、`chido_equipment_master.progression_value`、`chido_enemy_currency_master.drop_amount`、`chido_skill_master.learnable_level`、`chido_title_master.condition_value`、`chido_battle_participant.current_hp` / `total_damage_dealt`、`chido_effect_slip_damage_instance.status_attack_value`）は、単一行の取得か小さなマスタ表の走査であり、C#側で`BigInteger`として比較すれば足りる。生成列は持たせない。

**運用上の注意**

- **`(桁数, 辞書順)`が数値順に一致するのは値が非負のときのみ。** 対象2列はいずれも設計上UNSIGNEDである。負値を入れると順序が壊れる。
- **並べ替えは必ず`Chido.Data.Queries.RankingQueries`を経由する。** 桁数の項を書き忘れても例外にはならず、静かに辞書順（`"9" > "10"`）を返すため、並び順の知識をコード上の1箇所に閉じ込めている。
- **100桁を超える値は書き込み時に例外になる**（`BigIntegerToStringConverter`）。非STRICTモードのMySQLは超過分を静かに切り詰めるため、桁が落ちた値が正しい値として流通する事故を防いでいる。符号も1文字を占める。
- SQL側での加減算（`UPDATE ... SET amount = amount + X`）はできない。読み出して`BigInteger`で計算し、書き戻す。

### 割合値のスケールと命名規約（本改訂で確立）

戦闘システムドキュメント 2.2 の通り、割合が絡む数値はC#側で `Ratio` 構造体（permyriad、10000 = 100%）に統一して扱う。DB側の格納スケールもこれに合わせるが、**手動設定の容易さを優先して意図的に例外を設ける列がある**ため、サフィックスで区別する。

| サフィックス／列名 | 演算 | 格納スケール | 符号 | `Ratio` への変換 | 例 |
|---|---|---|---|---|---|
| `_shape` | 乗算 | **100 = 1.00** | 非負 | **しない** | `chido_enemy_master.hp_shape` |
| `_rate` | 乗算 | permyriad（10000 = 100%） | 用途による | **する** | `hp_rate`, `strength_rate`, `exp_rate`, `drop_rate`, `equip_rate`, `grant_rate`, `disable_rate`, `accuracy_rate`, `rarity_rate`, `fixed_rate`, `rate`, `effect_rate` |
| `_bonus_rate` | 加算 | permyriad | 符号あり | する | `chido_equipment_master.luck_bonus_rate` |
| `_bonus` | 加算 | 絶対値 | 符号あり | しない | `chido_equipment_master.speed_bonus` |
| `power` | 乗算 | **整数%** | **非負** | **しない** | `chido_skill_motion_attack_master.power`, `chido_skill_motion_heal_master.power`, `chido_effect_slip_damage_master.power` |
| `weight` | — | 相対重み（合計値に意味を持たない） | 非負 | しない | `chido_enemy_skills_master.weight` |

**`_shape` が例外である理由**: 種族値相当の手動設定値であり、`100` を等倍として読み書きする方が人力でのバランス調整に馴染む。permyriad との不整合は承知の上での判断。

**`power` が例外である理由**: 威力は計算に使われる値であると同時に、プレイヤーへ提示される表示情報でもある。意図的に小数精度を持たない整数%として扱い、`_rate` とは別概念に据える。

**`weight` が `Ratio` の対象外である理由**: 相対重みであり、合計値が特定の数値になることを前提としない。これに対し、残差に「該当なし」という意味を持たせる確率値（`equip_rate`、`rarity_rate`）は `_rate` として permyriad で表現する。

### 列挙値・ビット列・符号の使い分け

アイテム／スキル／敵／状態変化／装備の各マスタ設計を通じて確立した規約。

- **列挙値**（1つの状態のみを取る値。例: `rarity`, `motion_type`, `target_status`, `target_rule`, `action_pattern_type`, `affect_reason`, `learned_reason`, `item_usage_type`, `entity_type`, `status`, `end_reason`）→ `TINYINT UNSIGNED`
- **ビット列**（複数値の併存を許容するフラグ集合。例: `elements`, `effect_types`, `equip_parts`）→ `INT UNSIGNED`。C#側の`[Flags] enum`と1:1対応させる。
- **割合値の符号**: バフ／デバフ双方向（正負）を許容しうる値（例: `effect_rate`, `fixed_rate`, `rate`, `chido_equipment_master.*_rate`）は符号ありの`INT`。装備の補正値は他の計算式に渡す前提の中間値であるため、負値＝デメリット装備を許容する。最終ステータスそのものではない。
- **常に非負の割合値**（命中率・抽選率・確率等で、最終的な結果値として完結するもの。例: `accuracy_rate`, `drop_rate`, `disable_rate`, `grant_rate`, `equip_rate`, `rarity_rate`, `power`）→ permyriad の場合は `SMALLINT UNSIGNED`（0〜10000）。倍率として 6.55 倍を超えうる値（`strength_rate`, `exp_rate`）は `INT UNSIGNED`。
- **1つの親（可読キー）に対し複数行を許容するテーブル**は、`(親可読キー, 連番または種別)` の複合PKで表現する（例: `chido_skill_motion_master`, `chido_enemy_skills_master`, `chido_enemy_effects_master`, `chido_effect_status_modifier_master`, `chido_item_used_effect_master`, `chido_enemy_group_member_master`）
- **マスタ／インスタンスの分離**：マスタ側は「種別・属性など静的な性質」を持ち、インスタンス側は「発生時点でしか決まらない量」を持つ、という原則を状態変化・スリップダメージ等で踏襲する。ただし常に同一の値になる行（固定変動）はインスタンス側への複製を避けるため、マスタ側に値そのものを持たせる例外を認める（`chido_effect_status_modifier_master.fixed_rate`）。インフレ型のゲーム性上、複製コストの削減効果が長期的に大きいための判断。
- **表示用の分類とロジック上の真実の情報源が分離する値**は、表示用の列を「非正規化キャッシュ」として明示し、真実の情報源となるテーブル側との整合性維持をアプリ側の責務とする（例: `chido_effect_master.effect_types`とサブテーブル群、`chido_item_master.item_type`と`chido_item_used_effect_master`、`chido_field_enemy_group_master.rarity`と`chido_enemy_group_master.rarity`）。
- **標準処理では説明のつかない一点物の特殊処理が想定される値**は、`special_process_key VARCHAR(64) NULL`という識別子文字列の列を持たせる（例: `chido_skill_master.special_process_key`, `chido_item_master.special_process_key`）。値は実在するテーブルを指す物理的な外部キーではなく、アプリ側のディスパッチ処理（識別子文字列をキーとしたswitch相当の実装）を呼び出すための記号であり、NULL=標準処理のみで完結することを示す。実際の特殊処理の中身は、実際に必要になった段階で個別のテーブル・実装を用意する（本カラムはその存在を示す目印のみを今のうちから持たせるためのもの）。1行につき紐づけられる特殊処理は同時に1つまでとし、複数の特殊処理を併存させる必要が生じた場合は、その時点で改めて設計する。
- **「1つ以下」という制約は、可能な限りテーブル構造そのものから導く**。1プレイヤー1行（PKが`user_id`）とすることで、DB側の追加制約なしに一意性が保証される（例: `chido_player_title_display`, `chido_player_equipment_slot`, `chido_player_in_battle_session`）。同様に1チャンネル1行の`chido_channel_state`は「1チャンネルにアクティブなセッションは1つ以下」を導く。

### 非同期・飛び入り参加前提のバトル設計

戦闘に「参加者募集」フェーズは存在しない。敵出現後、プレイヤーが最初に戦闘行為（攻撃／スキル／戦闘用アイテム）をした時点で`chido_battle_session`にINSERTされる。

ターン進行は「プレイヤー1行動＝敵1行動」の**個別即応型**。参加者全体で共有する順番待ちキューは存在せず、各プレイヤーは他の参加者の行動状況と無関係にいつでも行動できる。1人のプレイヤーが行動すると、対応する敵から即座に1回の反撃が返る、という1対のやり取りとして扱う。「複数の敵がいる場合に誰を狙うか」も同様に各プレイヤーが個別に保持する（`chido_battle_participant.current_target_id`）。共有ターンキューがないのと同じ理由で、対象選択も他の参加者の状態とは無関係に、プレイヤーごとに独立して決定・変更できる。

このため、以下の概念は**意図的に持たない**：

- ラウンド／フェーズ（`ended_at`の有無で進行中／終了を表現する）
- イニシアティブ順・行動済みフラグ（共有ターンキューが存在しないため不要）
- 座席番号（seat_order）によるターン制御（あくまで表示順を保つ`joined_at`のみ残す）
- プレイヤーごとの行動間隔制限（連打対策）。原型のゲーム自体が連打前提であり、対策はサーバー管理者側の裁量（Discordのスローモード機能等）に委ねる方針とし、Core側には実装しない。

**プレイヤーは同時に複数の戦闘セッションへ参加できない。** 他チャンネルで戦闘中のプレイヤーが戦闘行為を試みた場合、その旨のメッセージを返して行動を拒否する。この制約は`chido_player_in_battle_session`（36番）が1プレイヤー1行であることによりテーブル構造から導かれる。

### 現在HPの真値と、戦闘ごとの全快

- 現在HPの真値は`chido_battle_participant.current_hp`のみ。プレイヤー単位の永続的な現在HPは保持しない。
- セッションへの参加時、`current_hp`はレベル・装備から算出した`MaxLife`（＝全快）で初期化される。**戦闘ごとに全快する**のは意図した仕様であり、戦闘外の回復手段は設計上存在しない。
- 戦闘中の現在HPは`MaxLife`を超えうる（回復効果、装備変更による最大HPの減少など）。**クランプは一切行わない。** オーバーヒール状態は、次のセッション参加時に`MaxLife`が書き込まれることで自然に解消される。
- `/status`は経験値・レベル・装備・称号といった永続情報のみを扱う。現在HP等のセッション情報は、将来的に「戦闘状況表示」に特化した別コマンドが担う想定（今後の拡張。具体化しない）。

### 排他制御とロックの正準順序（運用上の注意）

同一の敵に複数プレイヤーがほぼ同時に攻撃するケースが実運用上頻発するため、**悲観ロック**（`SELECT ... FOR UPDATE`）を用いる。ロック対象を個別テーブルごとに列挙するのではなく、**必ず行が存在する実体テーブルの1行をロックアンカーとして取得する**方式に統一する。これにより、トランザクション分離レベル（`REPEATABLE READ` / `READ COMMITTED`）への暗黙の依存を排除する。

デッドロックを構造的に排除するため、ロックの取得順序を以下に固定する。

1. `chido_player.user_id`（行動者本人。プレイヤーに関する排他のアンカー）
2. `chido_channel_state.channel_id`（チャンネルに関する排他のアンカー。セッション生成レースもここで直列化される）
3. `chido_battle_session.session_id`

上位の段を飛ばすことは順序違反ではない（例: 装備変更コマンドは ② を取らない）。

- **チャンネル行（②）が全戦闘行動の直列化点**である。これを保持している間は「1チャンネルにアクティブなセッションは1つ以下」が保証されるため、戦闘行動においてセッション行（③）のロックは冗長。③ が必要なのは、② を取得しない非チャンネル起点コマンド（装備変更等）との排他のみ。
- 戦闘行動は同一セッション内で完全に直列化される。7章の`Defer` → `Edit`（遅延応答）を前提とするため機能的な破綻はないが、同時行動人数に比例して最後のプレイヤーの応答待ち時間が伸びる点は承知の上で採用している。
- `chido_player_equipment_slot`・`chido_battle_participant`への明示的なロックは不要（上記アンカーに包摂される）。敵の装備は出現時に確定しセッション中に変化しないため、`chido_battle_enemy_equipment_slot`はロック対象外。プレイヤーと敵の装備構造は対称だが、ロック要件は意図的に非対称である。
- `chido_player_effect`への書き込み（`remaining_actions`の減衰、および解除モーションによる削除）は**チャンネル行（②）に包摂される**。対象が行動者本人か他の参加者かを問わない。**アンカーが ① ではなく ② である点に注意**: `target_rule = 味方` の解除・付与モーションは他プレイヤーの`chido_player_effect`を書き込むが、行動者は他プレイヤーの ① を取得しないため ① では不足する。② を飛ばす唯一の経路である装備変更は`chido_player_effect`を書かないため、② のみで安全が保たれる。
- 戦闘中の装備変更は**許容する**。ステータスはレベル・装備から毎回動的算出されるため、スナップショット用のカラムは不要。
- Discordのインタラクション応答は3秒以内に`DeferAsync()`等の一次応答が必須のため、ロック取得より**先に`DeferAsync()`を呼び**、ロック取得・計算完了後に`ModifyOriginalResponseAsync()`で結果を編集する。

### その他の運用上の注意（スキーマ外の決定事項）

- `chido_battle_participant`は戦闘終了後も物理削除しない前提。継続中の戦闘と終了済みの戦闘とで、目視デバッグの都合により将来的にテーブルを分ける可能性はあるが、「戦闘終了後も参照可能」という前提自体は変わらない（`chido_player_effect.granter_entity_id`が戦闘終了後も履歴的に参照可能である根拠となっている）。`chido_battle_enemy`も同様に物理削除しない。
- 敵が装着している装備がドロップする場合、`chido_battle_enemy_equipment`のインスタンスをそのまま所有者移転するのではなく、内容（`equip_key`等）を複製した新しいインスタンスとして`chido_player_equipment`に新規`instance_id`でINSERTする。同一の敵から複数プレイヤーが報酬を受け取りうるため、1つの装備インスタンスを複数プレイヤー間で共有できないことによる。
- セッション終了処理は、`end_reason`の値を問わず（`ChannelMissing`を含む）`chido_player_in_battle_session`の該当`session_id`行の一括削除を必ず伴う。これが漏れると、チャンネル消失時に`Defeated`だったプレイヤーが永久に他の戦闘へ参加できなくなる。

### 順序キーの選び方（本改訂で確立）

**時刻列を順序キーに流用しない。** `DATETIME(3)` は「同時に走らないこと」が保証されていても「別ミリ秒になること」は保証しない。一括INSERTでは確実に同値になり、直列化された処理でも同一ミリ秒に収まりうる。順序が意味を持つ場面では、順序を表す専用の列を持つ（`chido_battle_participant.display_order` がこの方針の適用例）。

**安定ソートが必要で、意味のある順序キーが存在しない場合は、決定的なキーを最終タイブレークに置く。** MySQL は `ORDER BY` で決まらない行順序を保証しないため、同値の行が並ぶ表示は再描画のたびに入れ替わりうる。意味を持たない列（`instance_id` 等）でも、決定的である限り最終タイブレークとして十分に機能する。表示順に求められるのは「意味のある順序」ではなく「安定していること」である。

### 状態変化の寿命に関する不変条件（本改訂で確立）

> すべての状態変化は、有限の行動数（`remaining_actions`）か、戦闘の終了（`clear_on_battle_end = 1`）か、そのいずれかによって必ず終わりが保証される。

| `clear_on_battle_end` | `duration_actions` | 意味 | 終わりの保証 |
|---|---|---|---|
| 1 | NULL | この戦闘の間ずっと | 戦闘終了 |
| 1 | 有限 | この戦闘の中で N 行動 | 両方 |
| 0 | 有限 | 戦闘を跨いで N 行動 | 行動数 |
| 0 | NULL | **禁止**（真に永久） | **なし** |

「真に永久」を禁じるのは、永久なステータス補正がレベルや装備や称号と同じ**プレイヤーの属性**であって、付与・解除というライフサイクルを持つインスタンスではないため。それを状態変化として持つと、加算合成される永続デバフが単調増加し、上限なくステータスを蝕む。この禁止1つでその暴走が構造的に閉じるため、状態変化の解除手段（10d）は救済措置ではなく純粋な表現力の追加という位置づけになる。

**「残りターン数」ではなく「残り有効行動数」である理由**: 減衰の契機は時間の経過ではなく、保持者がターンに関与したことである（時計ではなくカウンタの消費）。保持者にとって1ターンは自分の1行動そのものなので数値としては一致するが、「ターン」という語は「戦闘内の時計」という誤った含意を持ち込み、永続スコープへの適用可否の判断を誤らせる。UI 表示上の「ターン」は保持者の1行動と一致するため正確であり、そちらは丸めてよい。

### 補正値の合成：レイヤー内は加算、レイヤー間は乗算（本改訂で確立）

```
戦闘時ステータス = 基礎ステータス × 強さ倍率 × 装備補正(+%) × 状態変化補正(+%)
装備補正(+%)     = 1 + Σ(各スロットの補正値)
状態変化補正(+%) = 1 + Σ(各状態変化インスタンスの effect_rate)
```

レイヤーを分けて乗算するのは「片方の陳腐化を防ぐ」ためであり、この理由はレイヤー**間**にのみ当てはまる。同一レイヤー内の複数インスタンス（装備5スロット、併存する複数の状態変化）は加算合成する。

**帰結**: 加算合成では強力なデバフの累積により DEF 等が負値を取りうる（`-60%` が2つで `1 - 1.2 = -0.2`）。ダメージ計算式が `max(0, ...)` のクランプを維持している理由はこれである。

---

## 確定スキーマ

### 1. chido_player — プレイヤー基本情報

```sql
CREATE TABLE chido_player (
    user_id   BIGINT UNSIGNED NOT NULL PRIMARY KEY, -- Discordユーザーの永続ID（スノーフレーク）。
                                                    -- 常に行が存在するため、プレイヤーに関する悲観ロックのアンカーとして使用する（横断的な設計方針を参照）
    user_name VARCHAR(72)     NULL                  -- 表示名のキャッシュ。Discord APIから毎回引くとレイテンシが大きいため保持。将来的にニックネーム機能にも転用可能
);
```

### 2. chido_battle_status — 戦闘関連ステータス

```sql
CREATE TABLE chido_battle_status (
    user_id BIGINT UNSIGNED NOT NULL PRIMARY KEY, -- chido_player.user_id を参照
    exp     VARCHAR(100)     CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                                                  -- 経験値。レベルは √exp で算出。10進整数文字列。
                                                  -- 初期値は 1。0 だと level=0 となり、
                                                  -- 基礎ステータス = レベル × Scale × Shape により全ステータスが0になって成立しない
    exp_len TINYINT UNSIGNED AS (CHAR_LENGTH(exp)) STORED,
                                                  -- exp の桁数。ランキングの第1ソートキー。
                                                  -- 非負の正準10進文字列では (桁数, 辞書順) が数値順に一致する
    INDEX idx_exp_rank (exp_len, exp)             -- ORDER BY exp_len DESC, exp DESC で数値降順を得る
);
```

各種戦闘ステータス（攻撃力・防御力・素早さ等）はレベルから毎回算出するため、このテーブルには持たない。

**`exp` の初期値は `1`（レベル1）とする。** `chido_player` 登録時の INSERT でこの値を書き込む（DBの `DEFAULT` には委ねず、アプリ側の定数として明示的に書く。10.5 の「草原」固定と同じ前例）。`level = √exp` である以上、`exp = 0` はレベル0＝HP0・ATK0 を意味し、プレイヤーが成立しない（10.5 の累積敵レベル初期値が `1` であるのと同じ理由）。レベル導出は戦闘システム 2.3 の `level = max(1, floor(√exp))`（BigInteger整数平方根、`floor` クランプは `Level` 取得時点）を正とする。初期値1（正常系の保証）とクランプ下限1（異常系のフェイルセーフ）は冗長ではなく二重の防御であり、同一の定数（1）を参照する。

**`current_hp`は本改訂で削除した。** 現在HPの真値は`chido_battle_participant.current_hp`のみであり、戦闘ごとに全快する仕様（横断的な設計方針を参照）のもとでは、非戦闘時に保持すべき値が存在しないため。参加中セッションの参照は`chido_player_in_battle_session`（36番）が担う。

### 3. chido_battle_session — 戦闘セッション

```sql
CREATE TABLE chido_battle_session (
    session_id     BINARY(16)       NOT NULL PRIMARY KEY, -- 使い捨てGuid。プレイヤーの最初の戦闘行為時に新規発行される
    guild_id       BIGINT UNSIGNED  NOT NULL,             -- 戦闘が発生したDiscordサーバーID
    channel_id     BIGINT UNSIGNED  NOT NULL,             -- 戦闘が発生したチャンネルID。chido_channel_state.channel_id を参照
                                                          -- message_id は持たない（下記）
                                                          -- last_action_at は持たない（下記）
    created_at     DATETIME(3)      NOT NULL,             -- セッション開始時刻
    ended_at       DATETIME(3)      NULL,                 -- 終了時刻。NULL=進行中、NOT NULL=終了（phase列の代わりにこれで進行状態を表現する）
    end_reason     TINYINT UNSIGNED NULL                  -- 終了理由。ended_atがNULLの間は常にNULL。BattleEndReason:
                                                          --   0: PlayerVictory  敵側の生存参加者が Defeated により 0 になった
                                                          --   1: PlayerEscaped  プレイヤー側の生存参加者が Escaped により 0 になった
                                                          --   2: EnemyEscaped   敵側の生存参加者が Escaped により 0 になった
                                                          --   3: ChannelMissing チャンネル消失により継続不可能になった
);
```

**`last_action_at` を持たない（I-4・決定事項）**: 非同期設計では長時間放置そのものが許容されており、放置を理由にセッションを終了させる処理は今後も入らない（終了条件はチャンネルの存否）。全行動で更新が必要なのに誰も読まない `NOT NULL` 列は書き込みコストだけが残り、将来「時間で切る」実装を呼び込む余地にもなる。セッションの経過時間は `created_at`、最終活動時刻が必要になれば `chido_battle_log.created_at` の最大値で得られる。

**`message_id` を持たない（B-3・B-4・決定事項）**: 戦闘進捗は「1つの埋め込みメッセージを編集して表示する」方式を採らない。**1回の戦闘行動につき1つの新規メッセージを送り、行動レスポンスと進捗表示を同一メッセージへ集約する**（戦闘システム 3.1 参照）。編集し続ける単一の進捗メッセージが存在しないため、指し示す対象がそもそも無く、この列を読む経路も無い。`last_action_at` と同じく「全行動で更新しながら誰も読まない列」になるため置かない。

終了理由はトリガー発火時点で明示的に記録する。`chido_battle_participant.status`の分布からは、`PlayerEscaped`と`EnemyEscaped`（および`PlayerVictory`）を事後的に区別できないため（例: 敵が2体おり、1体が逃走・1体が撃破された場合）。

`end_reason`は次に出現する敵の抽選ロジックを分岐させる（`PlayerVictory`→通常抽選、`PlayerEscaped`→組のレアリティに応じた10.3の例外規則、`EnemyEscaped`→常に`Common`から再抽選）。

`PlayerVictory`以外の終了理由では、いかなる参加者も報酬を得られない。

### 4. chido_battle_participant — 戦闘参加者

```sql
CREATE TABLE chido_battle_participant (
    session_id        BINARY(16)       NOT NULL,               -- chido_battle_session.session_id を参照
    entity_id         BINARY(16)       NOT NULL,               -- 参加者インスタンスの使い捨てGuid（IEntity.Id）
    entity_type       TINYINT UNSIGNED NOT NULL,               -- 0: Player, 1: Enemy
    user_id           BIGINT UNSIGNED  NULL,                   -- entity_type=0 のとき必須。chido_player.user_id を参照
    enemy_id          BINARY(16)       NULL,                   -- entity_type=1 のとき必須。chido_battle_enemy.enemy_id を参照
    status            TINYINT UNSIGNED NOT NULL,               -- ParticipantStatus（0: Active, 1: Escaped, 2: Defeated）。
                                                               -- current_hp=0 からの間接判定ではなく状態そのものを一次情報として保持する。
                                                               -- entity_type を問わず全行に適用される（敵も戦闘離脱モーションにより Escaped になりうる）
    current_hp        VARCHAR(100)     NOT NULL,               -- 戦闘中の現在HP。現在HPの唯一の真値。参加時は MaxLife（全快）で初期化される。
                                                               -- MaxLife を超える値を取りうる（クランプしない）。
                                                               -- 「戦闘不能」の判定には使用しない（status 列が唯一の根拠）
    current_tp        SMALLINT UNSIGNED NOT NULL,              -- 現在のTP（0〜1000）。Player: 参加時0で初期化／Enemy: 出現時 chido_enemy_master.initial_tp で初期化。
                                                               -- 蓄積量（通常攻撃+100/防御+100/被攻撃時 floor(500×被ダメージ÷最大HP)）と上限1000はC#側の定数として保持する。
                                                               -- TP+100はAttack/Defendモーションの再生を契機とする。被攻撃TPの被ダメージは実効ダメージ（台帳計上値）。
                                                               -- SlipDamage被弾でもインスタンス単位で蓄積する。戦闘システム 4.4参照
    current_target_id BINARY(16)       NULL,                   -- 現在の攻撃対象。同一session内の他行のentity_idを参照。「誰を攻撃するか」を決める概念。
                                                               -- chido_skill_motion_master.target_rule = 敵 の解決規則が本列を読む（両者は上位・下位の関係であり、別概念ではない）。
                                                               -- Player: 対象enemyのentity_idが入る／Enemy: ゲームシステム上使用されず常にNULL。
                                                               -- 解決は初回既定・自動失効後の再選定を区別しない単一の導出関数で行う：
                                                               --   本列が非NULLかつ参照先が Active ならそれ、さもなくば Active な display_order 最小の敵。
                                                               -- 参加時は NULL のまま（初期化書き込みをしない）。後段に落ちた結果は本列へ書き戻す（方式b）。
                                                               -- 敵を [対象] 指定した場合の更新は、そのターンの反撃者確定より前に行う。戦闘システム 3.3参照
    rotation_index    TINYINT UNSIGNED  NOT NULL DEFAULT 0,    -- 敵のローテーション（action_pattern_type=2）の現在位置。出現時0で初期化。
                                                               -- 敵が行動するたびに（選択の成否・require_tpフォールバックの有無に関わらず）
                                                               -- (rotation_index + 1) % total で進める（total = chido_enemy_skills_master の登録行数、戦闘中は不変）。
                                                               -- 1対1では結果的に (turn-1) % total に一致するが、それは観測される従属式であって
                                                               -- 決定規則ではない（真実の情報源は本列。多対多で敵ごとに独立させるために列で持つ）。
                                                               -- Player およびローテ以外の敵では未使用（0のまま）。戦闘システム 4.2参照
    display_order     SMALLINT UNSIGNED NOT NULL,              -- 表示順。entity_type ごとに独立した番号空間を持つ。
                                                               -- Enemy : chido_channel_current_enemy.spawn_index（＝組の member_index）をそのまま複製する。
                                                               --         ターゲット自動再選定における「先頭の敵」の唯一の根拠（戦闘システム 3.3）
                                                               -- Player: セッション内の参加順。参加時に同一 (session_id, entity_type=0) の最大値+1 を採番する（初参加は 0）。
                                                               --         Discord埋め込みの表示順にのみ使用され、ターゲット選定には使用されない
    joined_at         DATETIME(3)      NOT NULL,               -- 参加時刻の記録。順序付けには使用しない（display_order がその責務を持つ）
    PRIMARY KEY (session_id, entity_id),
    UNIQUE KEY uk_display_order (session_id, entity_type, display_order),
    CHECK (
        (entity_type = 0 AND user_id IS NOT NULL AND enemy_id IS NULL) OR
        (entity_type = 1 AND user_id IS NULL AND enemy_id IS NOT NULL)
    )
);
```

`seat_order` や `initiative`（イニシアティブ順）、`has_acted`（行動済みフラグ）は、共有ターンキューを前提とした概念のため持たない。

**`display_order` は本改訂で追加した（旧: 未確定事項 E-6）。** 敵の「組」（42・43番）は全メンバーが同一トランザクションで一括INSERTされるため `joined_at` がミリ秒精度で同値になり、戦闘システム 3.3 のターゲット自動再選定（「`Participants`リスト内で`Active`を保つ先頭の敵」）が一意に定まらなかった。

**プレイヤー側にも実値を入れる理由**: 「ターゲット文脈でプレイヤーの値は使わない」＝「値が不要」ではない。この列は追加した瞬間に `joined_at` から表示順の責務を奪うため、プレイヤー側にも順序の根拠が必要になる。加えて `joined_at` は本質的に順序キーとして不適格である。参加処理はチャンネル行ロックで直列化されるが、**直列化は「同時に走らない」ことを保証するだけで、`DATETIME(3)` が別ミリ秒になることは保証しない**。組が発端ではあるが、原因は「時刻を順序キーに流用していたこと」自体であるため、双方で外す。

NULL 許容にしてプレイヤーを NULL とする案は、ソートが「NULL なら `joined_at`、非NULL なら本列」という二段の条件になって `joined_at` への依存が残り、NULLソート順のDB依存性も抱え込むため採らない。固定値0や全員同値も、MySQL が同値行の順序を保証しないため埋め込みの並びが揺れる。

**`entity_type` ごとに独立した番号空間を持つ理由**: 敵側は `member_index` → `spawn_index` → `display_order` が**恒等の複製**になり、値の再導出やオフセット加算が入らない。マスタの `member_index = 2` の敵は参加者行でも `2` であり、追跡が単純。単一空間にしてプレイヤーにオフセットを振ると、この対応が壊れ、かつ「プレイヤーは敵の後ろ」といった表示方針をスキーマに埋め込むことになる。埋め込みの並べ方はプレゼンテーション層の判断としてC#側に残す。

**型**: `member_index` は `TINYINT UNSIGNED` だが本列は `SMALLINT UNSIGNED`。組のメンバー数は255で頭打ちにして問題ないが、プレイヤー側は「1チャンネルの戦闘に**累計**何人が参加しうるか」であり（採番は `MAX+1` の累計値であって同時参加者数ではない）、非同期・飛び入り参加前提のゲームで255という上限を設ける必然性がないため。

**採番の安全性**: `MAX+1` は競合すると欠番や重複を生むが、参加処理はチャンネル行ロックを取得した内側で行われるため（正準ロック順序）、同一セッションへの同時参加は直列化される。`UNIQUE KEY uk_display_order` が最後の砦として機能する。

**欠番の許容**: `Escaped` / `Defeated` の行は物理削除されないため離脱者の番号は空かない。離脱後の再参加で新しい `entity_id` の行が生まれた場合は `MAX+1` により末尾に付く。順序としては「参加順」の意味が保たれるため、欠番の詰め直しは行わない。

### 5. chido_battle_log — 戦闘ログ

```sql
CREATE TABLE chido_battle_log (
    log_id      BIGINT UNSIGNED  AUTO_INCREMENT PRIMARY KEY, -- ログの連番ID
    session_id  BINARY(16)       NOT NULL,                   -- chido_battle_session.session_id を参照
    actor_id    BINARY(16)       NOT NULL,                   -- 行動主体のentity_id（chido_battle_participant.entity_id）。
                                                             -- SlipDamage による継続ダメージでは、被害者ではなく
                                                             -- chido_battle_effect.granter_entity_id（付与者）を記録する（下記参照）
    action_type TINYINT UNSIGNED NOT NULL,                   -- ActionType（Attack/Skill/Defend/Use/Escape）
    target_id   BINARY(16)       NULL,                       -- 対象のentity_id（対象がいない行動ではNULL）
    payload     JSON             NULL,                       -- ダメージ量等の詳細（DamageResult等をシリアライズ）。
                                                             -- 記録するダメージ値は「実効ダメージ」＝ min(最終ダメージ, 適用直前の現在HP) とする（下記参照）
    created_at  DATETIME(3)      NOT NULL,                   -- ログ発生時刻
    INDEX idx_session_log (session_id, log_id)               -- 「特定セッションのログを時系列で取得する」クエリの高速化用
);
```

非同期設計において、離席中に起きた出来事を後から追える点で特に価値が高いテーブル。報酬付与対象の判定（「セッション中に敵へ1回以上ダメージを与えたか」という行動実績）にも使用する。

**記録するダメージは「実効ダメージ」である（決定事項）**: `min(パイプラインの最終ダメージ, 適用直前の現在HP)`。生ダメージで積むと、経験値按分の分母（全プレイヤーの累積与ダメージ）が膨れ上がり、**オーバーキルが他人の取り分を破壊する**。例えば A が99ダメージ（敵HP100）を与え、B が残り1を10000ダメージで仕留めると、A の貢献率は 0.98% となり、99%の仕事をした A の取り分がほぼゼロになる。戦闘システム 6.2 参照。

**`SlipDamage` の `actor_id` は付与者である（決定事項）**: 継続ダメージは対象のターンに発動するため、素直に実装すると「敵が自分自身に与えたダメージ」になり、毒付与に徹したプレイヤーの貢献に一切計上されない。`chido_battle_effect.granter_entity_id` を `actor_id` として記録することで、経験値按分の分子・分母に正しく積まれる。

なお、累積与ダメージを本テーブルの `payload` の集計で得るか、`chido_battle_participant` に専用列を持たせるかは未確定（未確定事項 G-1 参照）。ログの粒度（1行=1ターンか1モーションか）も未確定（同 I-3）。

### 6. chido_battle_enemy — 戦闘中の敵の状態

```sql
CREATE TABLE chido_battle_enemy (
    enemy_id   BINARY(16)             NOT NULL PRIMARY KEY, -- 出現の都度新規発行される使い捨てGuid。1つのenemy_idにつきchido_battle_participant行は常に1つのみ
    master_key VARCHAR(64)            NOT NULL,             -- chido_enemy_master.enemy_key を参照。どの敵か（種別）を示す
    level      VARCHAR(100)           NOT NULL              -- 敵のレベル。10進整数文字列。出現時の chido_channel_state.cumulative_enemy_level をそのまま複製する。
                                                            -- 組の全メンバーが同一レベルとなる（メンバーごとのレベル差は設けない）。
                                                            -- 基本ステータスはプレイヤー同様レベルから動的算出するためこれ以外は持たない
);
```

`current_hp`は`chido_battle_participant`側に既に存在するため、ここには重複して持たない。

チャンネルへの紐付けは持たない。行は履歴として物理削除されないため、`channel_id`列を持たせても「現在出現中の敵」を識別できないことによる。現在出現中の敵は`chido_channel_current_enemy`（38番）が保持する。

### 7. chido_item_master — アイテムマスタ

```sql
CREATE TABLE chido_item_master (
    item_key            VARCHAR(64)      NOT NULL PRIMARY KEY, -- 可読キー
    name                VARCHAR(100)     NOT NULL,             -- 表示名
    item_type           TINYINT UNSIGNED NOT NULL,             -- アイテム種別（0: battle, 1: material, 2: collection, 3: skill_learning）。
                                                               -- battle は「戦闘ステータスに作用する戦闘用アイテム」を表し、Use アクションの対象になる。
                                                               -- 表示用カテゴリ。skill_learningの場合、chido_item_used_effect_masterに
                                                               -- item_usage_type=learn_skillの行が存在することの非正規化キャッシュ。
                                                               -- 真実の情報源はchido_item_used_effect_master側であり、
                                                               -- 両者の整合性を保つ責務はアプリ側にある（chido_effect_master.effect_typesと同じ運用）
    is_consumable       TINYINT UNSIGNED NOT NULL,             -- 消費アイテムか（0/1）。item_typeとは独立したフラグとして持つ
    description         VARCHAR(500)     NULL,                 -- 説明文
    special_process_key VARCHAR(64)      NULL                  -- 特殊処理呼び出し記号。NULL=標準処理（chido_item_used_effect_masterの定義通り、または無機能アイテムとして何も起きない）のみで完結。
                                                               -- 値がある場合、標準処理では説明のつかない専用ロジックがアプリ側に別途存在することを示す
                                                               -- （例: "joke_message_display" ＝ descriptionとは別の専用メッセージを使用時に表示する）
);
```

アイテム使用時の具体的な効果内容（スキル発動・スキル習得等）は`chido_item_used_effect_master`（24番）で管理する。`special_process_key`が設定されている場合、実際の処理内容は個別に実装される（横断的な設計方針を参照）。

### 8. chido_player_item — プレイヤー所持アイテム

```sql
CREATE TABLE chido_player_item (
    user_id  BIGINT UNSIGNED NOT NULL,           -- chido_player.user_id を参照
    item_key VARCHAR(64)     NOT NULL,           -- chido_item_master.item_key を参照
    quantity INT UNSIGNED    NOT NULL DEFAULT 0, -- 所持数
    PRIMARY KEY (user_id, item_key)
);
```

### 9. chido_skill_master — スキルマスタ

```sql
CREATE TABLE chido_skill_master (
    skill_key           VARCHAR(64)       NOT NULL PRIMARY KEY, -- 可読キー
    name                VARCHAR(100)      NOT NULL,             -- 表示名
    description         VARCHAR(500)      NULL,                 -- 説明文
    elements            INT UNSIGNED      NOT NULL,             -- スキル属性（ビット列、複数併存可）。
                                                                -- ダメージ計算には一切使用しない、UI表示専用の"見せかけ"の値。
                                                                -- ダメージ計算が参照するのは chido_skill_motion_attack_master.elements（モーション属性、攻撃モーションのみが持つ）。
                                                                -- モーション属性からの自動導出は行わず、手動設定を前提とする。
                                                                -- 攻撃モーションを持たないスキル（純回復等）でも本列は保持できる（完全な演出値になる）
    require_tp          SMALLINT UNSIGNED NOT NULL,             -- TP消費量（0-1000）。スキル発動時に消費する。
                                                                -- 回復モーションを含むスキルでは 200 以上とする（運用制約。戦闘システム 4.4 参照）。
                                                                -- 166 以下では被反撃だけでTPが自給でき、回復を毎ターン撃てるため回復威力の実用帯が消滅する
    learnable_level     VARCHAR(100)      NULL,               -- 習得レベル。NULL=レベルアップでは習得不可（アイテム消費等、他の手段でのみ習得可能）。
                                                                -- 10進整数文字列。小さなマスタ表であり、レベル閾値の判定はC#側でBigIntegerとして行う
    priority            INT               NOT NULL DEFAULT 0,   -- 行動優先度。行動順は OrderBy(priority) → ThenBy(Speed) → ThenBy(Random) で決まる（降順・先攻が先）。
                                                                -- 既定は 0（Attack・通常スキル）。Defend には正の値を与え、Speed に関わらず被弾前に構えを取れるようにする。
                                                                -- 戦闘システムドキュメント 4.1 を正とする
    special_process_key VARCHAR(64)       NULL                  -- 特殊処理呼び出し記号。NULL=標準の効果計算処理のみで完結。
                                                                -- （例: "coin_toss_currency_consumption" ＝ 所持金消費を攻撃力として扱う専用計算）
);
```

**通常攻撃（Attack）と防御（Defend）も、本テーブル上の通常のスキルエントリとして表現される。** Attackは「対象に威力100%の無属性物理攻撃」という単一モーション、Defendは被ダメージ軽減のみ（反撃モーションなし。実体は `target_rule = 自分自身`・`duration_actions = 1` のDRR付与モーション1つ。戦闘システム 4.2・5.4参照）。マスタデータはエンティティ種別（プレイヤー／敵）を問わず共通のため、Attackのスキルエントリはプレイヤー・敵間で単一の行として共有される。

この2つは全プレイヤーが習得手続きなしに常時使用でき、`chido_player_skill`（23番）の習得管理対象外として扱う必要があるが、その表現方法（フラグ列を設けるか、アプリ側の定数で決め打ちするか）は未確定（未確定事項 G-3 参照）。**なお Attack/Defend の `skill_key` は、習得管理除外に加えて、TP+100の契機判定（4.4）と `priority` 既定値の付与（Defendに正値、4.1）からも参照される共通の定数になった**ため、単なる表現様式の差ではなく、3つの参照点が同じ1箇所を指すための集約設計になっている（G-3の位置づけが変化。アプリ側定数として1箇所に集約するのが素直）。

**`range_type` は本改訂で削除し、`chido_skill_motion_master.target_rule` として移設した。** スキル単位で対象を1つしか持てないと、戦闘システム 5.3 が例に挙げる「自分を強化（無属性）」→「火属性の攻撃」という2モーション構成のスキルが表現できない（1モーション目の対象は自分、2モーション目の対象は敵）。ドレイン（敵を攻撃して自分を回復）も同様。これは将来の改善ではなく、**現行スキーマで表現できないものがあるという既存の欠陥**であった。

あわせて `range_type`（影響範囲種別）→ `target_rule`（対象の解決規則）に改称した。影響範囲は常に単体固定であり「範囲」という概念が存在しないこと、実体が「対象をどう解決するかの規則」であることによる。詳細は10番および戦闘システム 4.2 を参照。

### 10. chido_skill_motion_master — スキルモーション

本テーブルは**スーパータイプ**であり、`motion_type` を判別子として4つのサブタイプテーブル（10a・10b・10c・10d）を持つ（戦闘離脱のみ可変パラメータを持たずサブタイプなし）。全モーションが共通して持つ属性のみを本テーブルに置く。

```sql
CREATE TABLE chido_skill_motion_master (
    skill_key         VARCHAR(64)       NOT NULL,               -- chido_skill_master.skill_key を参照
    motion_index      TINYINT UNSIGNED  NOT NULL,               -- 再生順序。スキルはこの昇順にモーションを再生する
    motion_type       TINYINT UNSIGNED  NOT NULL,               -- モーション種別。サブタイプの判別子。子テーブルと1対1に対応する（離脱を除く）。
                                                                --   0: 攻撃           → chido_skill_motion_attack_master  (10a)
                                                                --   1: 回復           → chido_skill_motion_heal_master    (10b)
                                                                --   2: 状態変化付与   → chido_skill_motion_effect_master  (10c)
                                                                --   3: 戦闘離脱       → サブタイプなし（可変パラメータを持たない）
                                                                --   4: 状態変化解除   → chido_skill_motion_dispel_master  (10d)
    target_rule       TINYINT UNSIGNED  NOT NULL,               -- 対象の解決規則（0: 自分自身, 1: 味方, 2: 敵）。常に単体固定。
                                                                -- 「プレイヤーが選ぶ選択肢」ではなく「対象をどう解決するかの規則」であり、
                                                                --   自分自身 → 行動者そのもの（[対象] 指定があっても対象は変わらない）
                                                                --   敵       → chido_battle_participant.current_target_id を読む（初回既定・再選定を区別しない単一の導出関数。戦闘システム 3.3参照）
                                                                --   味方     → コマンドの [対象]。省略時は行動者自身。「味方」は自分自身を含む（戦闘システム 4.2参照）
                                                                -- 敵の味方対象モーションは chido_enemy_master.ally_target_rule で解決する（[対象] を持たないため）
                                                                -- 旧 chido_skill_master.range_type から移設・改称した（9番参照）
    accuracy_rate     SMALLINT UNSIGNED NOT NULL,               -- 命中率（攻撃・回復）／成功率（状態変化付与・解除・戦闘離脱）。permyriad（10000 = 100%）。
                                                                -- 4種すべてが使用する真の共通列であるため親に置く。
                                                                -- Attack/Defend のモーションは 10000 固定（運用制約。戦闘システム 4.4参照）。
                                                                -- 戦闘離脱の成功率は /escape（モーションを経由せず必ず成功する）とは別経路である
    accuracy_gate_group SMALLINT UNSIGNED NULL,                 -- 命中の依存グループ。NULL = 他モーションに依存せず単独で判定する（現行挙動）。
                                                                -- 同一 skill_key 内で同値の行が1グループを成し、motion_index 最小の行を先頭とする。
                                                                -- 先頭が効果適用に到達しなかった場合、同一グループの他メンバーは抽選を行わずスキップされる。
                                                                -- 先頭が到達した場合、他メンバーはそれぞれ自身の accuracy_rate を独立に抽選する。
                                                                -- グループ番号のスコープは skill_key 単位で、値そのものに意味はない。
                                                                -- 「攻撃が命中したら n% で毒付与」のような道連れ失敗を表現する（戦闘システム 4.2参照）。
                                                                -- 行間の関係を表す列であり単一行のCHECKでは守れない。整合性検証はアプリ側の責務
    PRIMARY KEY (skill_key, motion_index),
    UNIQUE KEY uk_subtype (skill_key, motion_index, motion_type)  -- サブタイプ側の複合FKの参照先。判別子ごと参照させるために必要
);
```

**親から `elements` を削除した（本改訂）**: 属性は攻撃モーションのみが持つため、`elements` は 10a（攻撃サブタイプ）へ移設した（戦闘システム 5.3・11.2-8 参照）。旧構成では親の `elements` を全 `motion_type` で `NOT NULL` としていたが、実際に参照するのは攻撃のみであり、回復・付与・解除・離脱では死に列だった。共通列は `target_rule` / `accuracy_rate` / `accuracy_gate_group` の3つになり、いずれも全モーションが実際に使う列で揃う。

### 10a. chido_skill_motion_attack_master — 攻撃（現在HPへの干渉）

```sql
CREATE TABLE chido_skill_motion_attack_master (
    skill_key    VARCHAR(64)       NOT NULL,
    motion_index TINYINT UNSIGNED  NOT NULL,
    motion_type  TINYINT UNSIGNED  NOT NULL,   -- 0（攻撃）
    attack_type  TINYINT UNSIGNED  NOT NULL,   -- Physical/Magical。参照する攻撃力を選択する
    power        SMALLINT UNSIGNED NOT NULL,   -- 威力。整数%（通常攻撃=100）。permyriad ではない点に注意。
                                               -- ダメージ = 攻撃力 × 威力 × 被防御係数(ATK÷(ATK+DEF))
    elements     INT UNSIGNED      NOT NULL,   -- モーション属性（ビット列、複数併存可）。攻撃モーションのみが持つ。
                                               -- ダメージ計算時に対象の実効属性との相性判定に使用される実効値。
                                               -- 0（無属性）が意味を持つ既定値（相性計算をスキップ＝全属性等倍。戦闘システム 2.4参照）
    PRIMARY KEY (skill_key, motion_index),
    CHECK (motion_type = 0),
    FOREIGN KEY (skill_key, motion_index, motion_type)
        REFERENCES chido_skill_motion_master (skill_key, motion_index, motion_type)
);
```

### 10b. chido_skill_motion_heal_master — 回復（現在HPへの干渉）

```sql
CREATE TABLE chido_skill_motion_heal_master (
    skill_key    VARCHAR(64)       NOT NULL,
    motion_index TINYINT UNSIGNED  NOT NULL,
    motion_type  TINYINT UNSIGNED  NOT NULL,   -- 1（回復）
    attack_type  TINYINT UNSIGNED  NOT NULL,   -- Physical/Magical。参照する攻撃力を選択する
    power        SMALLINT UNSIGNED NOT NULL,   -- 威力。整数%。回復量 = 攻撃力 × 威力（対象の防御力は影響しない ＝ 被防御係数1の攻撃）。
                                               -- 同格では被防御係数が0.5になるため、通常攻撃(100%)と釣り合う回復は威力50%。
                                               -- 回復モーションは elements（モーション属性）を持たない（戦闘システム 5.3参照）
    PRIMARY KEY (skill_key, motion_index),
    CHECK (motion_type = 1),
    FOREIGN KEY (skill_key, motion_index, motion_type)
        REFERENCES chido_skill_motion_master (skill_key, motion_index, motion_type)
);
```

**攻撃（10a）と回復（10b）を別サブタイプに分割した（本改訂）**: かつては両者を「現在HPへの干渉」として1テーブル（旧 10a）にまとめていたが、5.1 の被防御係数・5.2 のクリティカル・5.3 の属性相性が攻撃にのみ適用され、かつ属性（`elements`）を攻撃のみが持つため、列構成も意味論も異なる。`motion_type` が排他的な列挙値であるため、判別子を含む複合FKにより攻撃行が回復として登録される誤りをDBが弾ける。CHECK は旧 `IN (0, 1)` から各サブタイプの `= 0` / `= 1` に狭まる。将来「回復にも属性を持たせる」場合は 10b に列を1本足せば済み、他サブタイプに不使用列は波及しない。

### 10c. chido_skill_motion_effect_master — 状態変化付与

```sql
CREATE TABLE chido_skill_motion_effect_master (
    skill_key        VARCHAR(64)       NOT NULL,
    motion_index     TINYINT UNSIGNED  NOT NULL,
    motion_type      TINYINT UNSIGNED  NOT NULL,   -- 2（状態変化付与）
    effect_key       VARCHAR(64)       NOT NULL,   -- 付与する状態変化。chido_effect_master.effect_key を参照
    effect_rate      INT               NULL,       -- 効果量。permyriad、符号あり（デバフの負値を許容）。
                                                   -- 付与先の chido_effect_status_modifier_master.fixed_rate が NULL の行に対してのみ必須。
                                                   -- SlipDamage／DisableMove の効果量はそれぞれのマスタが持つため本列を使用しない
    attack_type      TINYINT UNSIGNED  NULL,       -- Physical/Magical。付与する状態変化が SlipDamage 成分を持つ場合に、
                                                   -- 継続ダメージが物理/魔法どちらの攻撃力を基準にするかを決める。
                                                   -- 付与時に chido_effect_slip_damage_instance.attack_type（22番）へ複製される。
                                                   -- 「同一の effect_key でも、付与した攻撃によって物理/魔法が動的に決まる」ための列であり、
                                                   -- 攻撃力の実値（status_attack_value）ではなく「どちらの攻撃力を読むか」という静的な性質を表す。
                                                   -- 参照先 effect_key が SlipDamage 成分を持つ行のみ NOT NULL（テーブルまたぎのためアプリ側の責務）。
                                                   -- SlipDamage 成分を持たない付与（StatusModifier / DisableMove / ElementGrant のみ）では NULL
    duration_actions SMALLINT UNSIGNED NULL,       -- 付与する状態変化の持続。「残り有効行動数」であり時計ではない。
                                                   -- chido_battle_effect.remaining_actions / chido_player_effect.remaining_actions の
                                                   -- 初期値として複製される。
                                                   -- NULL = 無期限（残り有効行動数という属性を持たない）。0 は取らない。
                                                   -- 付与先 effect の clear_on_battle_end = 0 の場合は NOT NULL 必須
                                                   -- （永続スコープの状態変化は必ず有限。テーブルまたぎのためアプリ側の責務）
    PRIMARY KEY (skill_key, motion_index),
    CHECK (motion_type = 2),
    CHECK (duration_actions IS NULL OR duration_actions >= 1),
    FOREIGN KEY (skill_key, motion_index, motion_type)
        REFERENCES chido_skill_motion_master (skill_key, motion_index, motion_type),
    FOREIGN KEY (effect_key) REFERENCES chido_effect_master (effect_key)
);
```

`effect_rate` の要否が複数テーブルにまたがる条件（付与先effectの `effect_types` × `fixed_rate` のNULL可否）で決まるため、CHECK制約では表現できず、整合性の担保はアプリ側の責務とする。サブタイプ化により `motion_type` による条件は消えたが、この条件は残る。

`duration_actions` の NOT NULL 制約（`clear_on_battle_end = 0` の場合）も同じくテーブルまたぎのためアプリ側の責務。

**`attack_type` を付与側に持つ理由（決定事項）**: `SlipDamage` の物理/魔法は、効果マスタ（17番）の静的な性質ではなく「それを付与した攻撃」によって動的に決まる。この動的決定のため `attack_type` は付与モーション側が保持し、付与時に 22番へ複製する。ダメージ計算時には対象の物理/魔法DEFのどちらを引くかを決めるためにインスタンス側の `attack_type` を参照する。**`auto` 付与側（14番）にも同じ列を対称に持たせる**（付与元は2つで両方が同じ列を持つ、という `effect_rate`／`duration_actions` の対称性に従う）。

**なぜサブタイプに分割せず NULL 許容列とするのか（決定事項）**: 10c は1行が1つの `effect_key` を指し、その `effect_key` は `effect_types`（ビット列）でマルチネイチャー（例: StatusModifier と SlipDamage を兼ねる）を取りうる。したがって「付与モーションを性質ごとに排他的に振り分ける判別子」が存在せず、10a〜10d のような `motion_type` 判別による分割（排他的な列挙値であることが前提）はこの下の階層では使えない。ゆえに `attack_type` は通常の NULL 許容列として持つ。NULL の煩雑さは手動設定時の二次的な問題にとどまり、分割の根拠にはならない。戦闘システムドキュメント 5.4 を正とする。

### 10d. chido_skill_motion_dispel_master — 状態変化解除

```sql
CREATE TABLE chido_skill_motion_dispel_master (
    skill_key    VARCHAR(64)      NOT NULL,
    motion_index TINYINT UNSIGNED NOT NULL,
    motion_type  TINYINT UNSIGNED NOT NULL,   -- 4（状態変化解除）
    effect_key   VARCHAR(64)      NOT NULL,   -- 解除対象。chido_effect_master.effect_key を参照。
                                              -- 対象が保持する全スコープ（chido_battle_effect + chido_player_effect）から、
                                              -- effect_key が一致する行をすべて削除する。
                                              -- granter_entity_id / grant_source_key / affect_reason は参照しない
                                              -- （解毒は毒の出所を問わないため。付与の重複判定とは意図的に非対称）
    PRIMARY KEY (skill_key, motion_index),
    CHECK (motion_type = 4),
    FOREIGN KEY (skill_key, motion_index, motion_type)
        REFERENCES chido_skill_motion_master (skill_key, motion_index, motion_type),
    FOREIGN KEY (effect_key) REFERENCES chido_effect_master (effect_key)
);
```

付与（10c）と解除（10d）は「状態変化への干渉」という点で同種に見えるが、解除の列構成は付与の真部分集合（`effect_key` のみ）であり、統合すると解除の行に `effect_rate` と `duration_actions` の NULL が2つ生まれるため分けている。

**解除モーションの追加により、リフレッシュ挙動がデータ側から合成可能になる。** 「解除 → 付与」の2モーション構成にすれば、`motion_index` 昇順の再生順により確実に上書きされる。これが、重複付与時の挙動として「拒否」をデフォルトに選べる根拠でもある（19番および戦闘システム 5.4 参照）。

### 10番テーブル群の設計方針

TPはコスト制のゲージ（下限0～上限1000）で、通常攻撃・防御・被攻撃時に蓄積し、スキル発動時に`require_tp`分を消費する。スキルは1効果（攻撃／回復／状態変化付与／戦闘離脱／状態変化解除）ごとに「モーション」として切り分けられ、`motion_index`昇順に再生されるモーショングループとして表現する。

`amount`列（旧: 威力または効果量を兼ねる単一列）を`power`／`effect_rate`に分割した。両者はスケール（整数% / permyriad）と符号が異なり、単一列で兼ねると`chido_effect_status_modifier_instance.rate`への書き込み時に100倍のスケール誤りを起こしうるため。

**なぜサブタイプ化するのか**: 単一テーブルでは `motion_type` によって使わない列が NULL になり、その NULL は「無期限」等の意味を持つ NULL と区別がつかない多義的なものだった。サブタイプ化により、

- **無意味な NULL が消える**。残る NULL は `effect_rate`（未使用）と `duration_actions`（無期限）の2つのみで、どちらも意味を持つ
- **NOT NULL / FK が宣言できる**。単一テーブルでは `power` を NULL 可にせざるを得ず、「`motion_type = 0` なのに `power IS NULL`」というゴミが表現可能だった。`effect_key` も同様に FK を張れなかった
- **判別子を含む複合FKにより、サブタイプの誤接続をDBが防ぐ**。`motion_type` が排他的な列挙値であるために使える手であり、`chido_effect_master` のサブタイプ（重複可のビット列）では使えない
- 将来 `motion_type` ごとのパラメータが増えても、他の種別に不使用列が波及しない

**サロゲートキーを導入しない理由**: 親に `(skill_key, motion_index)` という自然キーが既に存在し、これをサブテーブルの PK 兼 FK にできる。GUID を挟むと「親を作って ID を控え、子に書き写す」という管理ツール前提の運用になるが、自然キーなら手で書くのは可読キーと連番だけで済む。

**この様式の前例**: `chido_effect_master` + サブテーブル（16・17・18・45番）が同じパターンである。可読キーを親子で共有し、`effect_types` を「どのサブテーブルに行があるか」の非正規化キャッシュとして持つ。`motion_type` が `effect_types` に相当する判別子となる。

**DBで強制できない制約**: 「`motion_type = 0` なら必ず子行が存在する」（全体参加制約）はリレーショナルモデルでは表現できず、アプリ側の責務として残る。`motion_type` と子テーブルが1対1に対応するようになったため、検証クエリは種別ごとの単純な形になる。

```sql
-- 子行を欠いた親を検出する（種別ごと。UNION ALL で全種別を一括検査）
SELECT '0:attack' AS kind, m.skill_key, m.motion_index FROM chido_skill_motion_master m
LEFT JOIN chido_skill_motion_attack_master a USING (skill_key, motion_index)
WHERE m.motion_type = 0 AND a.skill_key IS NULL
UNION ALL
SELECT '1:heal', m.skill_key, m.motion_index FROM chido_skill_motion_master m
LEFT JOIN chido_skill_motion_heal_master h USING (skill_key, motion_index)
WHERE m.motion_type = 1 AND h.skill_key IS NULL
UNION ALL
SELECT '2:effect', m.skill_key, m.motion_index FROM chido_skill_motion_master m
LEFT JOIN chido_skill_motion_effect_master e USING (skill_key, motion_index)
WHERE m.motion_type = 2 AND e.skill_key IS NULL
UNION ALL
SELECT '4:dispel', m.skill_key, m.motion_index FROM chido_skill_motion_master m
LEFT JOIN chido_skill_motion_dispel_master d USING (skill_key, motion_index)
WHERE m.motion_type = 4 AND d.skill_key IS NULL;
-- motion_type = 3（戦闘離脱）はサブタイプを持たないため検査対象外
```

**`accuracy_gate_group` の整合性検証（アプリ側の責務）**: 本列は行間の関係を表すため、単一行のCHECK制約では守れない。同一 `skill_key` 内で同じ `accuracy_gate_group` を持つ行が2件以上あることを警告として検査する（1件のみは NULL と完全に等価であり、作成者の誤りの可能性が高い）。加えて、先頭が `motion_index` 最小の行で暗黙に決まるため、後からグループにより小さい `motion_index` の行を挿入すると先頭が無言で入れ替わる点は、スキルデータ編集時のチェック項目として扱う。

**読み取り**: 親＋子の LEFT JOIN で従来と同一の平坦な形が得られる。ただし攻撃（10a）と回復（10b）の分割により `attack_type` と `power` の列名が両テーブルに現れるため、`COALESCE` で畳む（`motion_type` が排他的なため同一行で両方が埋まることはない）。この JOIN を VIEW として定義するかは未確定（将来検討 J-2v 参照）。

```sql
SELECT m.skill_key, m.motion_index, m.motion_type,
       m.target_rule, m.accuracy_rate, m.accuracy_gate_group,
       COALESCE(a.attack_type, h.attack_type) AS attack_type,
       COALESCE(a.power,       h.power)       AS power,
       a.elements,                                        -- 攻撃以外は NULL（相性計算では 0 と同義。戦闘システム 2.4参照）
       e.effect_key AS grant_effect_key, e.effect_rate,
       e.attack_type AS grant_attack_type,                -- SlipDamage 用。10a/10b の attack_type とは別意味のため別名
       e.duration_actions,
       d.effect_key AS dispel_effect_key
FROM chido_skill_motion_master m
LEFT JOIN chido_skill_motion_attack_master a USING (skill_key, motion_index)
LEFT JOIN chido_skill_motion_heal_master   h USING (skill_key, motion_index)
LEFT JOIN chido_skill_motion_effect_master e USING (skill_key, motion_index)
LEFT JOIN chido_skill_motion_dispel_master d USING (skill_key, motion_index);
```

C#側で `a.elements` の NULL を `0` にマップすれば従来と同一挙動になるが、モーションモデル上は攻撃バリアントにのみ `Elements` を持たせるのが今回の決定の意図に忠実である（モデリングの選択でありスキーマの決定には影響しない）。

**戦闘離脱モーション**（`motion_type = 3`）は、`/escape`コマンドと同一の戦闘離脱処理をモーション経由で呼び出す。可変パラメータを持たないためサブタイプテーブルを作らない（`chido_effect_disable_move_master` と同じ基準）。離脱するのは `target_rule` により解決された対象であり、`target_rule = 敵` ならプレイヤーが敵を「追い払う」手段になる（`EnemyEscaped` の発火経路。3番の `end_reason` 参照）。`accuracy_rate` を持つため失敗しうる。`motion_index`昇順の再生において、戦闘離脱モーションより前のモーションはすべて通常通り再生された上で、**離脱に成功した場合に限り**、以降のモーションはショートサーキットされ再生されない。

### 11. chido_enemy_master — 敵マスタ

```sql
CREATE TABLE chido_enemy_master (
    enemy_key           VARCHAR(64)       NOT NULL PRIMARY KEY, -- 可読キー。chido_battle_enemy.master_key から参照される
    name                VARCHAR(100)      NOT NULL,             -- 表示名
    image_url           VARCHAR(500)      NULL,                 -- 敵画像URL。Discord埋め込みに使用
    rarity              TINYINT UNSIGNED  NOT NULL,             -- レアリティ（0: Common, 1: Uncommon, 2: Rare, 3: Mythic, 4: Hidden）。chido_equipment_master.rarityと共通のenum。
                                                                -- 個体の希少度を示す表示専用の値。敵の出現抽選には使用しない
                                                                -- （抽選のレアリティは chido_enemy_group_master.rarity が持つ）
    elements            INT UNSIGNED      NOT NULL,             -- 敵本体の属性（ビット列）。0 = 属性なし。
                                                                -- 実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」で算出される。
                                                                -- プレイヤーの本体属性は常に 0 であり（装備由来のみ）、対応する列を持たない
    hp_shape            SMALLINT UNSIGNED NOT NULL,             -- HP Shape（ポケモンの種族値に相当する正規化されたステータス倍率）。
                                                                -- 1.00 を 100 として格納（100=等倍）。permyriad ではない（命名規約を参照）。
                                                                -- 基礎ステータス = レベル × Scale（HP:12 / 攻撃・防御:8） × Shape
    patk_shape          SMALLINT UNSIGNED NOT NULL,             -- 物理攻撃力 Shape（同上）
    pdef_shape          SMALLINT UNSIGNED NOT NULL,             -- 物理防御力 Shape（同上）
    matk_shape          SMALLINT UNSIGNED NOT NULL,             -- 魔法攻撃力 Shape（同上）
    mdef_shape          SMALLINT UNSIGNED NOT NULL,             -- 魔法防御力 Shape（同上）
    strength_rate       INT UNSIGNED      NOT NULL,             -- 強さ倍率。permyriad（10000 = 等倍）。
                                                                -- 戦闘時ステータス = 基礎ステータス × 強さ倍率 × 装備補正 × 状態変化補正。
                                                                -- ボスとして出現させる場合などに 20000（=2倍）等を設定する。プレイヤーは常に等倍
    exp_rate            INT UNSIGNED      NOT NULL,             -- 経験値倍率。permyriad（10000 = 等倍）。strength_rate とは独立した値
    speed               SMALLINT UNSIGNED NOT NULL,             -- 素早さ。Scale × Shape の枠組みには含まれない固定値（プレイヤーは基本500）。
                                                                -- 変動要因は装備効果のみ（強さ倍率・状態変化補正の影響を受けない）
    initial_tp          SMALLINT UNSIGNED NOT NULL DEFAULT 0,   -- 出現時の初期TP（0〜1000）。chido_battle_participant.current_tp の初期値。
                                                                -- プレイヤーは常に0で初期化されるためこの非対称は意図的（現時点では拡張として据え置き）。
                                                                -- 初手から require_tp>0 のスキルを撃たせたい敵に、その分の初期値を持たせる。戦闘システム 4.2・4.4参照
    action_pattern_type TINYINT UNSIGNED  NOT NULL,             -- 行動パターン（0: 完全ランダム, 1: 重み付きランダム, 2: ローテーション）。スキルの選択規則
    ally_target_rule    TINYINT UNSIGNED  NOT NULL DEFAULT 0    -- 味方対象モーションの対象選択規則（種族単位）。action_pattern_type と対をなす（対象の選択規則）。
                                                                -- 敵には [対象] 入力も CurrentTarget の流用もないため、target_rule=味方 のモーションの対象を本列で解決する。
                                                                -- 候補集合は自軍側の Active な参加者（実行者自身を含む）。番号は族ごとに範囲を予約する（欠番を詰めない方針）。
                                                                --   ランダム系:  0=完全ランダム(自分含む), 1=自分以外からランダム(空なら自分), 2-9=予約
                                                                --   固定対象系: 10=display_order 0, 11=display_order 1, 12=display_order 2, 13-19=予約（将来）
                                                                --   情報参照系: 20=物理攻撃最大, 21=魔法攻撃最大, 22=物理防御最大, 23=魔法防御最大,
                                                                --               24=(CurrentLife/MaxLife) 最小(HP0除外), 25-29=予約（将来）
                                                                -- 現行実装は 0 / 1 / 24 の3規則のみ。他は予約値。値24は除算せず交差乗算で比較する。戦闘システム 4.2・11.2-5参照
);
```

`ally_target_rule` の固定対象系は「1番目」ではなく `display_order` の値で定義する（`display_order` は組の `member_index` の恒等複製で0起点であるため。3.3参照）。「ボスとその取り巻き」ではボスが `member_index = 0` となる想定で、`ally_target_rule = 10` が「ボスを狙う」に対応する。現行では 0 / 1 / 24 のみ実装し、固定対象系・その他情報参照系は将来拡張として予約値に割り当てる。

`*_rate`（旧: `hp_rate`等）を`*_shape`にリネームした。permyriad へ統一するにあたり、100基準で手動設定する種族値相当の値と、10000基準で`Ratio`に変換される割合値とを、列名で明確に区別するため。これにより`chido_equipment_master.*_rate`との符号・スケールの差異に関する注意書きが不要になる。

Luckは基本0%（プレイヤー・敵共通）であり、変動要因は装備効果のみのため、本テーブルに対応列を持たない。

### 12. chido_enemy_skills_master — 敵の使用スキル

```sql
CREATE TABLE chido_enemy_skills_master (
    enemy_key         VARCHAR(64)      NOT NULL,               -- chido_enemy_master.enemy_key を参照
    enemy_skill_index TINYINT UNSIGNED NOT NULL,               -- 再生・抽選順序
    skill_key         VARCHAR(64)      NOT NULL,               -- chido_skill_master.skill_key を参照
    weight            TINYINT UNSIGNED NOT NULL,               -- 抽選の相対重み。合計値に意味を持たず、Ratio への変換対象外。
                                                               -- action_pattern_type=1（重み付きランダム）でのみ参照される。
                                                               -- 0 = 抽選対象外。ただし完全ランダム／ローテーションでは本列自体が無視されるため、
                                                               -- weight=0 のスキルもそれらのパターンでは通常通り使用される（意図的な非対称）
    PRIMARY KEY (enemy_key, enemy_skill_index)
);
```

通常攻撃も他のスキルと同様、抽選プールの1エントリとして登録されうる（特別扱いはされない）。敵が1つもスキルを保有しない場合、または重み付きランダムで全スキルの`weight`が0で抽選プールが空になる場合は、例外処理として通常攻撃を行う。

**`require_tp` フォールバック（決定事項）**: 敵もTP（`chido_battle_participant.current_tp`、初期値は `chido_enemy_master.initial_tp`）を持ち、スキル発動時に `require_tp` を消費する。払えないスキルの扱いは `action_pattern_type` により分岐する。完全ランダム（0）・重み付きランダム（1）は「`require_tp` を満たすスキルのみで抽選プールを構成し、空なら通常攻撃へフォールバック」（1は残存エントリの `weight` をそのまま用いその合計で正規化）。ローテーション（2）は「ローテ順で選ばれたスキルが払えなければそのターンは通常攻撃へフォールバック（順番は飛ばさない）」。詳細は戦闘システムドキュメント 4.2 を正とする。

**登録された通常攻撃とフォールバックの通常攻撃は別物**である。前者は本テーブルにAttackの `skill_key` が登録された行で、ローテ枠を占め（`total` に数える）抽選候補になる。後者は払えなかった結果差し込まれる出力でローテ枠を持たない。いずれのAttackでもAttackモーションの再生を契機にTP+100が発火する（4.4参照）ため、両経路は同一点に合流する。

### 13. chido_enemy_loots_master — 敵のドロップテーブル

```sql
CREATE TABLE chido_enemy_loots_master (
    enemy_key VARCHAR(64)       NOT NULL,               -- chido_enemy_master.enemy_key を参照
    item_key  VARCHAR(64)       NOT NULL,               -- chido_item_master.item_key を参照
    quantity  SMALLINT UNSIGNED NOT NULL,               -- ドロップ数量
    drop_rate SMALLINT UNSIGNED NOT NULL,               -- ドロップ率。permyriad（10000 = 100%）。小数点2桁相当の精度を持つ
    PRIMARY KEY (enemy_key, item_key)
);
```

ドロップ判定は撃破に関与したプレイヤーごとに独立して行われる。判定手順（Luckによる再抽選を含む）は戦闘システムドキュメント 10.2 を正とする。

### 14. chido_enemy_effects_master — 敵の初期付与状態変化

```sql
CREATE TABLE chido_enemy_effects_master (
    enemy_key          VARCHAR(64)       NOT NULL,               -- chido_enemy_master.enemy_key を参照
    enemy_effect_index TINYINT UNSIGNED  NOT NULL,               -- 付与順序
    effect_key         VARCHAR(64)       NOT NULL,               -- chido_effect_master.effect_key を参照
    effect_rate        INT               NOT NULL,               -- 効果量。permyriad、符号あり（デバフの負値を許容）。
                                                                 -- chido_skill_motion_effect_master.effect_rate と同じ性質・同じ書き込み先
    attack_type        TINYINT UNSIGNED  NULL,                   -- Physical/Magical。付与する状態変化が SlipDamage 成分を持つ場合のみ NOT NULL。
                                                                 -- chido_skill_motion_effect_master.attack_type と同じ性質・同じ書き込み先（10c参照）。
                                                                 -- auto 付与の SlipDamage（「6行動で自滅する敵」等）が物理/魔法を決めるために必要。
                                                                 -- 付与時に chido_effect_slip_damage_instance.attack_type へ複製される。
                                                                 -- SlipDamage 成分を持たない付与では NULL（テーブルまたぎのためアプリ側の責務）
    duration_actions   SMALLINT UNSIGNED NULL,                   -- 持続。「残り有効行動数」であり時計ではない。
                                                                 -- chido_skill_motion_effect_master.duration_actions と同じ性質・同じ書き込み先。
                                                                 -- NULL = 無期限（戦闘終了まで持続。敵の効果は clear_on_battle_end によらず
                                                                 -- 戦闘終了時に除去される）。0 は取らない
    grant_rate         SMALLINT UNSIGNED NOT NULL,               -- 付与確率。permyriad（10000 = 100%）
    PRIMARY KEY (enemy_key, enemy_effect_index),
    UNIQUE KEY uk_enemy_effect (enemy_key, effect_key),          -- 同一の敵に同じ effect_key を2行定義できないようにする（下記参照）
    CHECK (duration_actions IS NULL OR duration_actions >= 1)
);
```

戦闘開始時に敵へ付与される状態変化を表す。`chido_battle_enemy.enemy_id`（出現インスタンス）ではなく`chido_enemy_master.enemy_key`（種別定義）に紐づく点に注意。実際の付与インスタンスは、戦闘開始時に`chido_battle_effect`へ書き込まれる（`affect_reason=auto`）。

**`duration_actions`・`attack_type` を本テーブルにも持つ理由**: 状態変化の付与元は「スキルモーション」と「敵の初期付与（auto）」の2つであり、付与時に決まる性質をモーション側にしか置かないと、auto付与された状態変化のその性質の出所が消える。`duration_actions` を持たないと「敵の初期状態変化は必ず無期限」という暗黙の制約が生まれ、`attack_type` を持たないと auto 付与の `SlipDamage` が物理/魔法を決められない。例えば「6行動で自滅する敵」は auto 付与の `SlipDamage` として表現するのが自然だが、そのとき持続（`duration_actions`）と物理/魔法（`attack_type`）が必要なのはモーション側ではなく auto 付与側である。`effect_rate` について既に確立している対称性（同じ性質・同じ書き込み先）に、`duration_actions` と `attack_type` も従う。

**`UNIQUE KEY uk_enemy_effect` を追加した理由**: PKが `(enemy_key, enemy_effect_index)` のみだと、同一の敵に同じ `effect_key` を2行定義できてしまう（`effect_rate` 違い等）。これらは戦闘開始時にすべて同じ `affect_reason = auto` / `granter = 自身` / `grant_source_key = NULL` で付与されるため、19番の重複判定キーが完全一致し、**2行目以降が実行時に黙って捨てられる**（重複時の挙動は「拒否」。19番参照）。データ入力ミスが実行時に無言で消えるのは最悪の失敗モードであるため、入力時に弾く。この列の組み合わせは NULL を含まないため、MySQL の UNIQUE がそのまま機能する。

なお、同一スキル内の複数モーションが同じ `effect_key` を付与するケース（例:「毒付与(2行動)→攻撃→毒付与(5行動)」）も同様に拒否される（`grant_source_key` は `skill_key` であって `motion_index` を含まないため）。これは拒否の意味論として一貫しているため仕様どおりだが、10c では `motion_type = 2` の行に限った制約になるため UNIQUE では弾けず、アプリ側の責務に残る。

### 15. chido_effect_master — 状態変化マスタ

```sql
CREATE TABLE chido_effect_master (
    effect_key          VARCHAR(64)      NOT NULL PRIMARY KEY, -- 可読キー。chido_skill_motion_effect_master.effect_key（10c）/ chido_skill_motion_dispel_master.effect_key（10d）/ chido_enemy_effects_master.effect_key から参照される
    name                VARCHAR(100)     NOT NULL,             -- 表示名
    description         VARCHAR(500)     NULL,                 -- 説明文
    effect_types        INT UNSIGNED     NOT NULL,             -- 保有効果種別（ビット列 / C#側は[Flags] enum）。
                                                               -- StatusModifier / SlipDamage / DisableMove / ElementGrant。
                                                               -- 各サブテーブルの行の有無に対応する非正規化キャッシュ（検索高速化と表示用途を兼ねる）
    clear_on_battle_end TINYINT UNSIGNED NOT NULL              -- 戦闘終了時に解除するか（0/1）。書き込み先の判定に使用：
                                                               -- Player: 1のとき chido_battle_effect（戦闘終了時に除去）／0のとき chido_player_effect（永続化）
                                                               -- Enemy: この値に関わらず常に chido_battle_effect（敵は出現の都度使い捨てのため永続化する意味を持たない）
);
```

状態変化は効果種別ごとにサブテーブル（16〜18番、45番）へ分割する。1つの状態変化が複数の効果種別・複数行を持ちうるため、`effect_types`が実際にどのサブテーブルに行を持つかを保証する責務はアプリ側にある（サブテーブル側を真実の情報源とし、`effect_types`はその参照結果のキャッシュという位置づけ）。実際に付与された状態変化の実値は`chido_battle_effect`／`chido_player_effect`（19・20番）以下のインスタンス側テーブル群で管理する。

**持続条件**は「残りターン数」によるカウントダウン方式。該当ターンにおける両エンティティの行動がすべて終了した直後、ターン終了の直前に残りターン数を-1し、0に達した時点で状態変化は消失する。

**発動タイミング**: `DisableMove`（行動不能）は対象が行動しようとした時点で判定する。`SlipDamage`（継続ダメージ）は対象のスキル再生が終了した直後に発動する（Escapeを除く全ActionTypeがスキル発動に収束するため、ActionTypeを問わず一律に適用できる）。

### 16. chido_effect_status_modifier_master — 状態変化：ステータス変動

```sql
CREATE TABLE chido_effect_status_modifier_master (
    effect_key    VARCHAR(64)      NOT NULL, -- chido_effect_master.effect_key を参照
    target_status TINYINT UNSIGNED NOT NULL, -- 対象ステータス（HP/物理攻撃/物理防御/魔法攻撃/魔法防御/素早さ/運/ダメージ軽減率...）。
                                             -- 現時点で変動対象は HP・物理/魔法攻撃・物理/魔法防御、およびダメージ軽減率（DRR）。Speed/Luck は対象外。
                                             -- ただし将来的に Speed/Luck を解禁する可能性があるため、対象ステータスを決め打ちした設計にはしない。
                                             -- ★DRR（ダメージ軽減率）は本列の一値として編入する（下記参照）。
                                             --   HP/攻撃/防御を指す行は 2.3 の状態変化補正倍率（1 + Σr）として乗算レイヤーに入るが、
                                             --   DRR を指す行は 2.3 の乗算レイヤーには入らず、Σr を (10000 - Σr)/10000 の形で
                                             --   ダメージパイプラインの PostDefense に供給する（合成の意味が異なる。アプリ側で分岐）
    fixed_rate    INT              NULL,     -- 固定変動率。permyriad、符号あり。
                                             -- NOT NULL=マスタ定義の固定値（例: 常に+50% → 5000／防御 Defend の DRR 50% → 5000）
                                             -- ／NULL=不定値（適用時にインスタンス側が変動率を保持する）
    PRIMARY KEY (effect_key, target_status)
);
```

1つの`effect_key`が複数の`target_status`を同時に変動させるケースを想定し、複合PKで複数行を許容する。`fixed_rate`がNULLの行のみ、インスタンス側（`chido_effect_status_modifier_instance`、21番）で実際の変動率を個別に保持する必要がある。

**ダメージ軽減率（DRR）の編入（決定事項）**: 防御（Defend）が付与する「被ダメージ軽減率」は、専用サブテーブルを新設せず本テーブルの `target_status` の一値として表現する。permyriad 符号あり。「装備でしか動かないpermyriad」であるLuckに対し、DRRは「状態変化でしか動かないpermyriad」とみなせる。

- **合成の意味が既存の `target_status` と異なる**: HP/攻撃/防御を指す行は、レイヤー内加算の結果 `1 + Σr` を 2.3 の状態変化補正倍率として乗算レイヤーへ供給する。一方DRRを指す行は、加算結果 `Σr` を `(10000 − Σr) ÷ 10000` の係数としてダメージパイプラインの PostDefense（戦闘システム 5.1参照）へ供給し、**2.3 の乗算レイヤーには一切入らない**。同じ21番 `rate` を読みながら、`target_status` がDRRを指す行だけ合成の意味が違う、という分岐がアプリ側に生まれる。これは許容する。
- **Defendの表現**: Defendの軽減率50%は固定値のため `fixed_rate = 5000` の固定値行として持つ（21番インスタンスを要しない。127行の「固定変動はマスタ側に値を持たせる例外」に合致）。DRRという性質は不定値も許容するが、Defendという具体的スキルは固定値表現でよい。
- **適用対象・下限**: DRRは攻撃モーション由来のダメージにのみ乗り、回復・`SlipDamage` には乗らない。合成係数は途中でクランプせず、最終ダメージの「最低1」で吸収する（戦闘システム 5.1参照）。

### 17. chido_effect_slip_damage_master — 状態変化：継続ダメージ

```sql
CREATE TABLE chido_effect_slip_damage_master (
    effect_key VARCHAR(64)       NOT NULL PRIMARY KEY, -- chido_effect_master.effect_key を参照
    elements   INT UNSIGNED      NOT NULL,             -- 攻撃属性（ビット列、複数併存可。chido_skill_master.elements / chido_skill_motion_attack_master.elements と命名・表現を統一）。
                                                       -- マスタ由来のため付与後も不変であり、スナップショット対象ではない
    power      SMALLINT UNSIGNED NOT NULL              -- 威力。整数%。非負。chido_skill_motion_attack_master.power と同一の概念・同一のスケール。
                                                       -- リジェネ的な表現が必要になった場合は本テーブルを流用せず別テーブルを新設する
);
```

**列名を `element` → `elements`（複数形）に改称した**。ビット列で複数属性を保持しうる実体と、`chido_skill_master.elements` / `chido_skill_motion_attack_master.elements` との命名統一のため。

`SlipDamage` のダメージ算出は攻撃モーションと同型（対象DEF・属性相性を考慮、最低1、クリティカルなし・DRRなし）で、戦闘システムドキュメント 5.1 のスリップパイプラインを通す。攻撃種別（Physical/Magical、`attack_type`）と、ダメージ算出の基準となる攻撃力の実値（`status_attack_value`）は、付与時点で決まる値のためインスタンス側（`chido_effect_slip_damage_instance`、22番）が保持する。`attack_type` の出所は付与モーション（10c／14番）であり、付与時に22番へ複製される。

### 18. chido_effect_disable_move_master — 状態変化：行動不能

```sql
CREATE TABLE chido_effect_disable_move_master (
    effect_key   VARCHAR(64)       NOT NULL PRIMARY KEY, -- chido_effect_master.effect_key を参照
    disable_rate SMALLINT UNSIGNED NOT NULL              -- 行動不能率。permyriad（0〜10000）。
                                                         -- 付与時に固定せず、保持者が行動しようとするたびに引く確率（A-7）。
                                                         -- 成立時にスキップされるのはスキル1本ぶんのモーション再生のみ。
                                                         -- ターン消費・TP蓄積・相手の反撃・残り有効行動数の減衰は成否によらず常に行われる
                                                         -- （ただし TP+100 はモーション再生を契機とするため成立時は発生しない）。
                                                         -- 併存する複数インスタンスは instance_id 昇順に独立抽選し最初の成功で打ち切る。
                                                         -- 戦闘システム 5.4 を正とする
);
```

確率のみで完結する効果のため、対応するインスタンス側テーブルは存在しない。

### 19. chido_battle_effect — 状態変化保持（戦闘内スコープ）

```sql
CREATE TABLE chido_battle_effect (
    instance_id       BINARY(16)       NOT NULL PRIMARY KEY, -- 使い捨てGuid。1回の付与ごとに新規発行
    entity_id         BINARY(16)       NOT NULL,             -- chido_battle_participant.entity_id を参照。効果保持者（Player/Enemy両方あり得る）
    effect_key        VARCHAR(64)      NOT NULL,             -- chido_effect_master.effect_key を参照
    affect_reason     TINYINT UNSIGNED NOT NULL,             -- 付与要因（0: skill, 1: auto）
    granter_entity_id BINARY(16)       NOT NULL,             -- 付与者のentity_id。auto付与時はentity_idと同値（自己付与）
    grant_source_key  VARCHAR(64)      NULL,                 -- 識別キー。skill付与時はskill_key。auto付与時はNULL（付与元がスキルでないことを示す）。
                                                             -- affect_reason は本列が「何のキーであるか」を示す型タグであり、本列からは導出できない
    remaining_actions SMALLINT UNSIGNED NULL                  -- 残り有効行動数。付与元（10c または 14番）の duration_actions を複製して初期化する。
                                                             -- 保持者が1ターンに関与するごとに -1 し、0 に達した時点で消失する。
                                                             -- 減衰の契機は時間の経過ではなく保持者がターンに関与したこと（＝時計ではなくカウンタの消費）。
                                                             -- NULL = 無期限。SQLのNULL伝播により -1 の対象からも消失判定からも自動的に外れる
                                                             -- （NULL - 1 = NULL、WHERE remaining_actions = 0 は NULL に一致しない）
);
```

`chido_effect_master.clear_on_battle_end=1`の効果（Player/Enemy問わず）と、Enemyの全ての効果（`clear_on_battle_end`の値に関わらず）はここに書き込まれ、戦闘終了時に除去される。

**型について**: `SMALLINT UNSIGNED`（0〜65535）。旧 `TINYINT UNSIGNED` は上限255で、実運用で設定しうる値（実績として最大9999）に届かない。マスタ側の `duration_actions` とは複製関係にあるため必ず同一の型とする。

**デクリメントと削除は同一トランザクション内で行う**（`remaining_actions = 0` の行が他のトランザクションから観測されないようにするため）。

**重複付与時の挙動（アプリ側担保）**: **拒否**。モーションは実行され `accuracy_rate` の判定も行われるが、状態変化の付与のみがスキップされ、既存インスタンスの `remaining_actions` は変更されない（延長しない）。DBレベルでの重複禁止は設けない（挙動仕様は戦闘システムドキュメント 5.4 参照）。

**重複の判定キー**: `entity_id` + `effect_key` + `affect_reason` + `granter_entity_id` + `grant_source_key` の5値。判定キーが一致しない同種効果は併存する（例: 複数の敵から受けた毒はそれぞれ併存する）。

**DBの UNIQUE では守れない**: `grant_source_key` は `affect_reason = auto` のとき NULL を取り、MySQL の UNIQUE は NULL を互いに異なる値として扱うため、NULL の行は何行でも入る。アプリ側担保は消極的な選択ではなく、この判定キーでは唯一の選択肢である。

**比較は NULL安全等価（`<=>`）で行うこと**: `grant_source_key = NULL` は決して一致しないため、素直にパラメータ化すると**auto付与の状態変化だけが無制限に重複する**という、テストで気づきにくいバグになる。

**解除モーション（10d）による物理削除が発生する**。`effect_key` が一致する行をすべて削除する（付与の重複判定とは異なり、`granter_entity_id` / `grant_source_key` / `affect_reason` は参照しない）。

### 20. chido_player_effect — 状態変化保持（永続スコープ）

```sql
CREATE TABLE chido_player_effect (
    instance_id       BINARY(16)       NOT NULL PRIMARY KEY, -- 使い捨てGuid。1回の付与ごとに新規発行
    user_id           BIGINT UNSIGNED  NOT NULL,             -- chido_player.user_id を参照。効果保持者（Playerのみ。Enemyは出現の都度使い捨てのインスタンスであり永続効果を持つ意味がない）
    effect_key        VARCHAR(64)      NOT NULL,             -- chido_effect_master.effect_key を参照
    affect_reason     TINYINT UNSIGNED NOT NULL,             -- 付与要因（0: skill, 1: auto）
    granter_entity_id BINARY(16)       NOT NULL,             -- 付与時点における付与者のentity_id（履歴的参照）。chido_battle_participantの行は戦闘終了後も物理削除されない前提のため参照可能。
                                                             -- 【重要】重複付与の一意性判定には使用しない（下記参照）
    grant_source_key  VARCHAR(64)      NULL,                 -- 識別キー。skill付与時はskill_key。auto付与時はNULL（付与元がスキルでないことを示す）
    remaining_actions SMALLINT UNSIGNED NOT NULL             -- 残り有効行動数。保持者が1ターンに関与するごとに -1 し、0 で消滅する。
                                                             -- 戦闘の境界では減衰も消滅もしない（戦闘を跨いで持続する）。
                                                             -- NOT NULL: 永続スコープの効果は必ず有限でなければならない
                                                             -- （終わりを保証するものが行動数しかないため）
);
```

Playerの`clear_on_battle_end=0`の効果のみがここに書き込まれる。付与先が`chido_battle_effect`／`chido_player_effect`のどちらになるかは、付与時点の`entity_type`と`clear_on_battle_end`の組み合わせでその場で確定するため、戦闘終了時に一方から他方へ行を移し替える処理は発生しない。

**`remaining_actions` が NOT NULL である理由（本改訂で追加）**: `remaining_actions` は「残り有効行動数」であり、戦闘の境界を何も参照しない。したがって「戦闘終了時に消さない」ことと「減衰を続ける」ことだけで、**戦闘を跨ぎつつ有限**という効果が新しい列なしで表現できる。`clear_on_battle_end` というフラグは、その反対側（戦闘を跨ぐ状態変化）が存在してはじめて意味を持つ。

`NULL`（無期限）を許すと「真に永久」な効果が表現可能になるが、これは禁止する。真に永久なステータス補正は、レベルや装備や称号と同じ**プレイヤーの属性**であって、付与・解除というライフサイクルを持つインスタンスではない。それを状態変化として持つと、加算合成（戦闘システム 2.3参照）される永続デバフが単調増加し、上限なくステータスを蝕む。

> **不変条件**: すべての状態変化は、有限の行動数か、戦闘の終了か、そのいずれかによって必ず終わりが保証される。

**減衰はその場で行う**: 戦闘中、プレイヤーが1行動するたびに本テーブルの `remaining_actions` を直接減衰させる。「戦闘開始時に `chido_battle_effect` へ複製し、終了時に書き戻す」という作業コピー方式は採らない。セッションは非同期・長期間開きっぱなしになりうるため（戦闘システム 4.3参照）、書き戻しの契機が来る保証がなく、永続効果の真値が無期限に戦闘テーブルに人質に取られるためである。ロックはチャンネル行に包摂される（横断的な設計方針および戦闘システム 7.2参照）。

**重複の判定キー**: `user_id` + `effect_key` + `affect_reason` + `grant_source_key` の4値。**`granter_entity_id` を含めない**。同 IDは `chido_battle_participant.entity_id`（セッションごとに発行される使い捨てGuid）であり、セッションをまたぐ本テーブルの一意性判定に用いると、同じ敵種と戦うたびに `granter` が異なるため常に「重複ではない」と判定され、判定が機能しない。`granter_entity_id` を判定に含める意味は「複数の敵から同時に毒を受ける」＝付与者が同じ戦場に同時に存在するという構造があるからであり、永続スコープにその構造はない。

**解除モーション（10d）による物理削除が発生する**。19番と同じ規則。

**`/status` の表示対象である**: 本テーブルは戦闘外にも存在するプレイヤー単位の永続情報であるため、`/status`（永続情報のみを扱う）の責務に含まれる。戦闘中でないプレイヤーが、自分に何が残り何行動効いているかを知る手段が他にない（戦闘システム 9.1参照）。

### 21. chido_effect_status_modifier_instance — インスタンス側：ステータス変動

```sql
CREATE TABLE chido_effect_status_modifier_instance (
    instance_id   BINARY(16)       NOT NULL, -- chido_battle_effect.instance_id または chido_player_effect.instance_id を参照
    target_status TINYINT UNSIGNED NOT NULL, -- chido_effect_status_modifier_master.target_status に対応
    rate          INT              NOT NULL, -- 実際の変動率。permyriad、符号あり。
                                             -- chido_effect_status_modifier_master.fixed_rate が NULL の行のみここに実値を持つ。
                                             -- 値の出所は chido_skill_motion_effect_master.effect_rate または chido_enemy_effects_master.effect_rate
    PRIMARY KEY (instance_id, target_status)
);
```

`effect_key`は持たせない。`instance_id`から親テーブル（`chido_battle_effect`／`chido_player_effect`）経由で一意に辿れ、エンティティ単位で状態変化を種別フィルタする要件も現時点で想定されないため。

### 22. chido_effect_slip_damage_instance — インスタンス側：継続ダメージ

```sql
CREATE TABLE chido_effect_slip_damage_instance (
    instance_id         BINARY(16)       NOT NULL PRIMARY KEY, -- chido_battle_effect.instance_id または chido_player_effect.instance_id を参照
    attack_type         TINYINT UNSIGNED NOT NULL,             -- Physical/Magical。付与モーション（chido_skill_motion_effect_master.attack_type / 10c、
                                                               -- または auto 付与の chido_enemy_effects_master.attack_type / 14番）から複製した静的な性質。
                                                               -- ダメージ計算時に対象の物理/魔法DEFのどちらを引くかを決めるために保持し続ける
    status_attack_value VARCHAR(100)     NOT NULL              -- 付与時点の攻撃力実値のスナップショット。BigInteger前提のためVARCHAR(100)。
                                                               -- attack_type が指す側（物理/魔法）の付与者ATK（付与時の StatusModifier 込み）を格納する
);
```

**`attack_type` の出所（コメント訂正）**: `attack_type` は「付与時点の術者のステータスに依存する量」ではなく、付与モーション（10c／14番）から複製される**静的な性質**である（術者依存なのは `status_attack_value` の実値のみ）。列を22番に保持し続けるのは、ダメージ計算時に対象の物理/魔法DEFのどちらを引くかを決めるために必要だからである。戦闘システムドキュメント 5.4 を正とする。

21・22番はいずれも`chido_battle_effect`と`chido_player_effect`の**両方**の`instance_id`を受け入れる共有テーブルである。GUIDのため衝突せず、親がどちらのテーブルかをサブテーブル側で区別する必要がない。ただし親が2テーブルに分かれるため`instance_id`への`FOREIGN KEY`制約は張れない（MySQLのFKは単一テーブルしか参照できない）。本スキーマ全体がコメントベースの参照で統一されているため、既存の設計方針からの逸脱ではない。`chido_effect_disable_move_master`に対応するインスタンス側テーブルは存在しない（確率のみで完結するため）。

### 23. chido_player_skill — プレイヤー習得スキル

```sql
CREATE TABLE chido_player_skill (
    user_id        BIGINT UNSIGNED  NOT NULL, -- chido_player.user_id を参照
    skill_key      VARCHAR(64)      NOT NULL, -- chido_skill_master.skill_key を参照
    learned_reason TINYINT UNSIGNED NOT NULL, -- 習得理由（0: level, 1: item, 2: cheat）
    PRIMARY KEY (user_id, skill_key)
);
```

`level`はレベルアップ時の自動習得（`chido_skill_master.learnable_level`が条件）、`item`はアイテム消費による習得（`chido_item_used_effect_master.item_usage_type=learn_skill`が対応）、`cheat`は管理者コマンドによる付与を表す。

装備限定スキルは本テーブルに保持せず、装備側から動的に参照する（参照経路のテーブル設計は将来検討。将来検討 H-1 参照）。通常攻撃（Attack）と防御（Defend）は習得管理の対象外（未確定事項 G-3 参照）。

### 24. chido_item_used_effect_master — アイテム使用効果

```sql
CREATE TABLE chido_item_used_effect_master (
    item_key        VARCHAR(64)      NOT NULL, -- chido_item_master.item_key を参照
    usage_index     TINYINT UNSIGNED NOT NULL, -- 効果の連番。use_skill(スキル発動)は常に1件のみ、learn_skill(スキル習得)は複数件を許容
    item_usage_type TINYINT UNSIGNED NOT NULL, -- アイテム効果種別（0: use_skill, 1: learn_skill。今後拡張予定）
    skill_key       VARCHAR(64)      NULL,     -- chido_skill_master.skill_key を参照。use_skill/learn_skillで使用
    PRIMARY KEY (item_key, usage_index)
);
```

アイテム使用時の具体的な効果を統括するテーブル。戦闘用アイテムの効果（`use_skill`：習得状況に関わらず特定スキルを発動）とスキル習得アイテムの効果（`learn_skill`）の両方をここで表現し、スキル発動ロジックを`chido_skill_motion_master`側にそのまま乗せられるようにする。`item_usage_type`は今後拡張予定のため`skill_key`は他の効果種別を見据えてNULL許容としている。

戦闘用アイテム（`chido_item_master.item_type=0`）の使用は、対象が自分や味方であっても`CurrentTarget`からの反撃とセットの通常行動として処理される（回復アイテムを使ったターンも無防備になる）。

### 25. chido_equipment_master — 装備マスタ

```sql
CREATE TABLE chido_equipment_master (
    equip_key         VARCHAR(64)      NOT NULL PRIMARY KEY, -- 可読キー
    name              VARCHAR(100)     NOT NULL,             -- 表示名
    equip_parts       INT UNSIGNED     NOT NULL,             -- 装備可能パーツ（ビット列。weapon/head/chest/legs/accessory）
    rarity            TINYINT UNSIGNED NOT NULL,             -- 装備レアリティ（0～4）。chido_enemy_master.rarityと共通のenum。同一進行度内での強さの序列付けに使用
    elements          INT UNSIGNED     NOT NULL,             -- 装備が付与する属性（ビット列）。0 = 属性なし。
                                                             -- プレイヤーの本体属性は装備由来のみであり、装着中の全スロットの elements の OR で決まる。
                                                             -- 多くの装備は 0 を設定する運用を想定（属性を持つ装備は部位を限定するなど、手動でのバランス調整に委ねる）
    progression_value VARCHAR(100)     NOT NULL,              -- レベルに対する想定進行度 P(level) の結果値のみを格納（例: Lv5000でP(5000)=60）。
                                                             -- レアリティ補正(*1.2^rarity)や各ステータス補正の乗算はアプリ側で都度算出する。
                                                             -- 手動設定される基礎値。10進整数文字列であり、SQL側でのソートは不要
    hp_rate           INT              NOT NULL,             -- HP補正値。permyriad、符号あり（10000=等倍、0=このステータスに無効果、負値=デメリット装備）
    patk_rate         INT              NOT NULL,             -- 物理攻撃力補正値（同上）
    pdef_rate         INT              NOT NULL,             -- 物理防御力補正値（同上）
    matk_rate         INT              NOT NULL,             -- 魔法攻撃力補正値（同上）
    mdef_rate         INT              NOT NULL,             -- 魔法防御力補正値（同上）
    speed_bonus       INT              NOT NULL,             -- 素早さ固定変動値。絶対値の加減算（例: +50 / -30）。Ratio への変換対象外
    luck_bonus_rate   INT              NOT NULL              -- 運補正値。permyriad、符号あり。乗算ではなく%ポイントの加算（例: +5% → 500）
);
```

HP・物理攻撃・物理防御・魔法攻撃・魔法防御の各ステータスは`P(level) * (1.2^rarity) * 補正値`で最終値を算出する想定。SpeedとLuckは上記の乗算構造の対象外で、Speedは固定加算の整数、Luckは permyriad の加算値として扱う。

`luck_bonus`を`luck_bonus_rate`にリネームした。`speed_bonus`（絶対値）と`luck_bonus`（permyriad）が同じ`_bonus`サフィックスで異なるスケールを指していたため。

戦闘中の装備変更は許容される。ステータスは毎回レベル・装備から動的算出されるため、変更は即座に反映される。ただし敵の装備は出現時に確定しセッション中に変化しない（意図的な非対称）。

### 26. chido_player_equipment — 装備所持状況

```sql
CREATE TABLE chido_player_equipment (
    instance_id BINARY(16)      NOT NULL PRIMARY KEY, -- 使い捨てGuid。装備を入手する都度新規発行される
    user_id     BIGINT UNSIGNED NOT NULL,             -- chido_player.user_id を参照。所有者
    equip_key   VARCHAR(64)     NOT NULL,             -- chido_equipment_master.equip_key を参照
    INDEX idx_user_equip (user_id, equip_key)         -- 所持装備一覧の取得、および同一装備の所持数集計に使用
);
```

同種の装備を複数所持できること、および将来的な個体差・強化付与の余地を見込み、インスタンス単位の行として管理する。所持数が必要な場合は`COUNT(*)`で導出し、`chido_player_item.quantity`のような専用カラムは持たない。

### 27. chido_player_equipment_slot — 装備装着状況

```sql
CREATE TABLE chido_player_equipment_slot (
    user_id                BIGINT UNSIGNED NOT NULL PRIMARY KEY, -- chido_player.user_id を参照
    weapon_instance_id     BINARY(16)      NULL,                 -- chido_player_equipment.instance_id を参照。武器スロット
    head_instance_id       BINARY(16)      NULL,                 -- 頭防具スロット
    chest_instance_id      BINARY(16)      NULL,                 -- 胴防具スロット
    legs_instance_id       BINARY(16)      NULL,                 -- 脚防具スロット
    accessory1_instance_id BINARY(16)      NULL                  -- アクセサリスロット1
);
```

1プレイヤー1行、スロットごとに1カラムを持つ構造。`chido_equipment_master.equip_parts`のビット単位はスロットの種別（候補）を表すものであり、物理カラムと1対1対応する保証はない（例: 1つの装備が複数スロットのいずれかを選んで装着できる、択一の候補提示）。装備がどのスロットに属するかは`equip_parts`を制約条件としてアプリ側が解決する（`effect_types`等と同じ、非正規化キャッシュ／整合性はアプリ側の責務という位置づけ）。

`accessory1_instance_id`は将来的な「アクセサリー2」追加を見越した番号付き命名。追加時は`accessory2_instance_id`列を足すのみで完結し、既存列のリネームは発生しない。weapon/head/chest/legsは複数枠化の想定がないため番号なしのままとしている。

本テーブルへの明示的な悲観ロックは不要（`chido_player.user_id`のロックアンカーに包摂される。横断的な設計方針を参照）。

### 28. chido_enemy_equipment_master — 敵の装備マスタ

```sql
CREATE TABLE chido_enemy_equipment_master (
    enemy_key             VARCHAR(64)       NOT NULL,               -- chido_enemy_master.enemy_key を参照
    enemy_equipment_index TINYINT UNSIGNED  NOT NULL,               -- 抽選候補の連番
    equip_key             VARCHAR(64)       NOT NULL,               -- chido_equipment_master.equip_key を参照
    equip_rate            SMALLINT UNSIGNED NOT NULL,               -- 装着確率。permyriad（10000 = 100%）。
                                                                    -- 同一スロット内の候補の合計が 10000 未満の場合、残差は「そのスロットに装備なし」を選ぶ暗黙の重みとして扱う。
                                                                    -- 残差に意味を持つ確率値であるため weight（相対重み）ではなく _rate として表現する
    drop_rate             SMALLINT UNSIGNED NOT NULL,               -- ドロップ率。permyriad。equip_rate とは独立した確率値
    PRIMARY KEY (enemy_key, enemy_equipment_index)
);
```

どのスロットに属するかを示す列は持たない。各候補がどのスロットに対応するかは`equip_key`経由で`chido_equipment_master.equip_parts`を参照すれば判定できるため、ここで重複して持たせていない（`chido_enemy_skills_master`と同型の設計判断）。

`equip_rate`（旧: `weight`）は、スロットごとに独立して抽選を行うための確率値。合計が10000を超えた場合は100%基準を放棄し、候補間の相対比率のみによる重み付き抽選にフォールバックする。この計算は複数行にまたがる集計を要するためDB側のCHECK制約では強制できず、整合性の判定・フォールバック処理の実行はアプリ側の責務とする。

`drop_rate`は`equip_rate`とは別軸の値である。「そもそも装備を着けている確率」と「その装備を着けた状態で敵が撃破された場合にドロップする確率」は独立して判定される。

### 29. chido_battle_enemy_equipment — 敵の装備インスタンス（戦闘内スコープ）

```sql
CREATE TABLE chido_battle_enemy_equipment (
    instance_id BINARY(16)  NOT NULL PRIMARY KEY, -- 使い捨てGuid。敵の出現(spawn)時、chido_enemy_equipment_masterの抽選結果に基づき新規発行される
    enemy_id    BINARY(16)  NOT NULL,             -- chido_battle_enemy.enemy_id を参照
    equip_key   VARCHAR(64) NOT NULL,             -- chido_equipment_master.equip_key を参照
    INDEX idx_enemy (enemy_id)                    -- 敵の所持装備一覧の取得に使用
);
```

`chido_player_equipment`と同型（instance_id / 所有者ID / equip_key）だが、`chido_battle_enemy`自体が戦闘スコープの一時的な実体であるのに合わせ、こちらも戦闘スコープの別テーブルとして持つ（`chido_battle_effect`と`chido_player_effect`のスコープ分割と同じ考え方）。GUIDを採用しているため、将来「個体差」用のサブテーブルを追加する際は`chido_player_equipment.instance_id`と本テーブルの`instance_id`の両方を受け入れる共有テーブルとして設計できる（21・22番の`instance_id`共有パターンと同じ理屈）。

### 30. chido_battle_enemy_equipment_slot — 敵の装着状況

```sql
CREATE TABLE chido_battle_enemy_equipment_slot (
    enemy_id               BINARY(16) NOT NULL PRIMARY KEY, -- chido_battle_enemy.enemy_id を参照
    weapon_instance_id     BINARY(16) NULL,                 -- chido_battle_enemy_equipment.instance_id を参照
    head_instance_id       BINARY(16) NULL,                 -- 頭防具スロット
    chest_instance_id      BINARY(16) NULL,                 -- 胴防具スロット
    legs_instance_id       BINARY(16) NULL,                 -- 脚防具スロット
    accessory1_instance_id BINARY(16) NULL                  -- アクセサリスロット1
);
```

`chido_player_equipment_slot`と完全に対称な構造。プレイヤーと敵を共通の戦闘システムで扱う思想に基づき、両者の装備構造を可能な限り対等にしている。`accessory2`はプレイヤー側にも存在しないため、敵側にも現時点では設けていない（追加時は両テーブル同時に列追加する運用）。

ただし敵の装備は出現時の抽選で確定し、セッション中に変化しない。したがって本テーブルは悲観ロックの対象外である（プレイヤー側との意図的な非対称）。

### 31. chido_player_currency — プレイヤー所持金

```sql
CREATE TABLE chido_player_currency (
    user_id    BIGINT UNSIGNED  NOT NULL PRIMARY KEY, -- chido_player.user_id を参照
    amount     VARCHAR(100)     CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
                                                      -- 所持金額。10進整数文字列（chido_battle_status.expと同じ判断基準）
    amount_len TINYINT UNSIGNED AS (CHAR_LENGTH(amount)) STORED,
                                                      -- amount の桁数。ランキングの第1ソートキー
    INDEX idx_amount_rank (amount_len, amount)        -- ORDER BY amount_len DESC, amount DESC で数値降順を得る
);
```

将来的に通貨単位を増やす拡張が考えられるが、その場合は本テーブルにカラムを追加する運用とする（`chido_player_equipment_slot`のスロット追加と同じ考え方）。**金額は10進整数文字列であるため`UPDATE ... SET amount = amount ± X`という加減算はできない。** 読み出して`BigInteger`で計算し書き戻す形になり、同時更新の直列化は正準ロック順序のアンカー（`chido_player.user_id`）が担う。

### 32. chido_enemy_currency_master — 敵ドロップ金額マスタ

```sql
CREATE TABLE chido_enemy_currency_master (
    enemy_key   VARCHAR(64)            NOT NULL PRIMARY KEY, -- chido_enemy_master.enemy_key を参照
    drop_amount VARCHAR(100)           NOT NULL              -- 撃破時に確定でドロップする金額（固定値、抽選なし）。手動設定される基礎値。10進整数文字列（chido_equipment_master.progression_valueと同じ判断基準）
);
```

`chido_player_currency.amount`の型とは独立した判断（手動設定される基礎値であり、蓄積後の所持金額そのものではないため）。将来通貨単位が増える場合は、こちらにも対応するドロップ額カラムを追加する。

### 33. chido_title_master — 称号マスタ

```sql
CREATE TABLE chido_title_master (
    title_key        VARCHAR(64)            NOT NULL PRIMARY KEY, -- 可読キー
    name             VARCHAR(100)           NOT NULL,             -- 表示名
    emoji            VARCHAR(64)            NOT NULL,             -- 表示用絵文字。Unicode文字、またはDiscordカスタム絵文字の完成済みタグ文字列(<:name:id>)をそのまま格納
    acquisition_type TINYINT UNSIGNED       NOT NULL,             -- 入手条件種別（0: 特定アイテム獲得, 1: 特定敵撃破, 2: レベル到達, 3: 所持金額到達）。今後拡張予定
    condition_key    VARCHAR(64)            NULL,                 -- 判定値(識別ID形式)。acquisition_type=0→item_key, 1→enemy_keyを参照（参照先はacquisition_typeにより分岐）
    condition_value  VARCHAR(100)           NULL                  -- 判定値(数値)。acquisition_type=2→レベル閾値、3→所持金額閾値。10進整数文字列であり、比較対象（exp由来のレベル、chido_player_currency.amount）と型を揃えている。閾値判定はC#側でBigIntegerとして行う
);
```

入手条件に応じてアプリ側が`condition_key`／`condition_value`のいずれかと照らし合わせ、称号獲得処理を行う。特殊な取得条件（例: プレイヤーと敵が同時に戦闘不能になる等）は、`acquisition_type`に新しい値を追加したうえで両判定値カラムをNULLのまま扱い、ハードコード実装として個別に対応する想定（`chido_item_used_effect_master.item_usage_type`と同じ「今後拡張予定」の扱い）。

### 34. chido_player_title — 称号所持状況

```sql
CREATE TABLE chido_player_title (
    user_id   BIGINT UNSIGNED NOT NULL, -- chido_player.user_id を参照
    title_key VARCHAR(64)     NOT NULL, -- chido_title_master.title_key を参照
    PRIMARY KEY (user_id, title_key)
);
```

`chido_player_item`と同型の複合PKだが、称号は獲得済みか否かの二値であり個数を持たないため`quantity`列は不要。入手経路も`chido_title_master.acquisition_type`で一意に決まるため、`chido_player_skill.learned_reason`のような記録列は設けない。

### 35. chido_player_title_display — 表示中の称号

```sql
CREATE TABLE chido_player_title_display (
    user_id   BIGINT UNSIGNED NOT NULL PRIMARY KEY, -- chido_player.user_id を参照
    title_key VARCHAR(64)     NULL                  -- chido_player_title.title_key を参照。NULL=称号を表示しない
);
```

`chido_player_equipment_slot`と同じ考え方（1プレイヤー1行、単一カラムで現在値を保持）。この形により「表示中の称号は常に1つ以下」という制約がテーブル構造そのものから導かれ、DB側で追加の制約を持つ必要がない。実際に表示可能なのは`chido_player_title`に存在する（＝獲得済みの）称号に限られるが、その整合性の維持はアプリ側の責務とする。

---

## 確定スキーマ（本改訂で新設）

### 36. chido_player_in_battle_session — 参加中の戦闘セッション

```sql
CREATE TABLE chido_player_in_battle_session (
    user_id    BIGINT UNSIGNED NOT NULL PRIMARY KEY, -- chido_player.user_id を参照。
                                                     -- 1プレイヤー1行という構造により「同時参加は1セッションまで」がテーブル構造から導かれる
                                                     -- （chido_player_title_display と同じ設計パターン）。
                                                     -- 行の不在＝非戦闘中を意味し、NULL 表現を持たない
    session_id BINARY(16)      NOT NULL,             -- chido_battle_session.session_id を参照
    entity_id  BINARY(16)      NOT NULL,             -- chido_battle_participant.entity_id を参照。
                                                     -- (session_id, entity_id) によるPK直引きを可能にするための非正規化。
                                                     -- これがないと chido_battle_participant に (session_id, user_id) の追加インデックスが必要になる
    INDEX idx_session (session_id)                   -- セッション終了時の一括削除に使用
);
```

参加判定（全戦闘コマンドの入口で毎回走る最ホットパス）を`user_id`のPK直引きで解決するため、PKは`user_id`とする。`(session_id, user_id)`の複合PKでは「1プレイヤー1セッション」の一意性が保証されず、かつ参加判定が全走査になる。

**行の削除タイミング**

| 契機 | 削除範囲 |
|---|---|
| 自身が `Escaped`（`/escape` または戦闘離脱モーション） | 当該`user_id`の行のみ |
| セッション終了（`end_reason` を問わず、`ChannelMissing` を含む） | `session_id` で一括 |
| 自身が `Defeated` になる | **削除しない**（拘束継続） |

行を削除しないことで`Defeated`のプレイヤーの拘束を継続する。拘束の解除契機とその意義は戦闘システムドキュメント 4.3 を参照。

### 37. chido_channel_state — チャンネル単位の永続状態

```sql
CREATE TABLE chido_channel_state (
    channel_id             BIGINT UNSIGNED        NOT NULL PRIMARY KEY, -- DiscordチャンネルID。
                                                                        -- 行の存在自体が「このチャンネルは戦闘チャンネルである」ことを意味する。
                                                                        -- 常に行が存在するため、チャンネルに関する悲観ロックのアンカーとして使用する
    current_field_key      VARCHAR(64)            NOT NULL,             -- chido_field_master.field_key を参照。現在のフィールド
    cumulative_enemy_level VARCHAR(100)           NOT NULL,             -- 累積敵レベル。初期値 1。敵の組を撃破するたびに +1（減少しない）。10進整数文字列。
                                                                        -- 出現する敵の level にそのまま複製される。
                                                                        -- この値が 2500 の倍数に達するたびにフィールドが切り替わる（専用カウンターは持たない）
    current_session_id     BINARY(16)             NULL,                 -- chido_battle_session.session_id を参照。NULL=進行中のセッションなし。
                                                                        -- 1チャンネル1行という構造により「アクティブなセッションは1つ以下」が導かれ、
                                                                        -- セッション生成レースを本行のロックで直列化できる
    current_group_key      VARCHAR(64)            NULL,                 -- chido_enemy_group_master.group_key を参照。現在出現中の組。NULL=未抽選（初期化直後）
    current_rarity         TINYINT UNSIGNED       NULL                  -- 現在出現中の組のレアリティ。NULL=未抽選（初期化直後）
);
```

**現在出現中の組を記録する理由（第5次改訂）**: 戦闘システム 10.3 の次の出現の計画は、`PlayerEscaped` のときに直前の組のレアリティで分岐し、`Common`/`Uncommon` であれば**同一の `group_key` を再出現させる**。したがって「直前に何が出ていたか」を知らなければ計画そのものが立たない。

どちらも**出現中の敵からは逆引きできない**。`chido_channel_current_enemy` から辿れるのは `chido_battle_enemy.master_key`（メンバーの種族キー）であり、同じメンバー構成の組が複数あれば組は一意に定まらない。レアリティも `chido_field_enemy_group_master` は「(フィールド, レアリティ) → 組」の対応であり、同じ組が複数のフィールド・レアリティに登録されうるため逆引きは一意にならない。

Phase 9b の実装時に、戦闘システムドキュメントが要求する値をDB設計側が保持していないことが判明したため、**戦闘ロジックの正は戦闘システムドキュメント**という優先順位に従い本表に2列を追加した（マイグレーション `AddChannelCurrentGroup`）。

行は戦闘チャンネル初期化コマンドの実行時に`INSERT`される。PK重複により再実行は失敗し、これが冪等性を担保する（初期化は「既にあるものを消去して作り直す」機能を伴わない）。

`ChannelMissing`によるセッション終了時、本行と`chido_channel_current_enemy`の行を削除する。Discordのチャンネルは復活せずIDも再利用されないため、累積敵レベルが失われることは問題にならない（10.1の「減少しない」規定とも矛盾しない）。

### 38. chido_channel_current_enemy — 現在出現中の敵

```sql
CREATE TABLE chido_channel_current_enemy (
    channel_id  BIGINT UNSIGNED  NOT NULL,               -- chido_channel_state.channel_id を参照
    spawn_index TINYINT UNSIGNED NOT NULL,               -- 組内の出現順。chido_enemy_group_member_master.member_index を引き継ぐ
    enemy_id    BINARY(16)       NOT NULL,               -- chido_battle_enemy.enemy_id を参照
    PRIMARY KEY (channel_id, spawn_index)
);
```

「今このチャンネルに出現している敵の集合」を表す。`chido_battle_enemy`は履歴として物理削除されないため、そちらに`channel_id`列を持たせても現在出現中の敵を識別できないことによる。

行の生存期間は「敵の組が出現してから、セッションが終了して次の組に入れ替わるまで」。セッション終了時に旧行を削除し、次の組の行を`INSERT`する。更新は常に`chido_channel_state`のロック配下で行われる。`enemy_id`は出現の都度発行される使い捨てGUIDであり、同一インスタンスが複数チャンネルに現れないことはアプリ側で担保する（DB制約は設けない）。

**書き込みは常に新規インスタンス（B-9・決定事項）**: `PlayerVictory`（次の組）・`PlayerEscaped` の「同一の組を再出現」・`EnemyEscaped` の再抽選のいずれでも、`enemy_id` は新規発行され、`chido_battle_enemy` に HP全快・装備再抽選・状態変化（`auto` 付与）再適用の新インスタンスが生成される。「前のインスタンスを引き継ぐ」経路は存在しない（前組が同一 `group_key` の場合も再生成する）。戦闘システム 10.3 を正とする。

セッションがまだ存在しない状態（初期化直後、および次の組が出現してから誰も行動していない状態）でも敵は存在するため、本テーブルは`session_id`を持たない。

### 39. chido_field_master — フィールドマスタ

```sql
CREATE TABLE chido_field_master (
    field_key VARCHAR(64)  NOT NULL PRIMARY KEY, -- 可読キー（例: 'grassland'）
    name      VARCHAR(100) NOT NULL              -- 表示名（例: '草原'）
);
```

最初のフィールドは「草原」固定。`chido_channel_state.current_field_key`のDEFAULT値としては表現せず、アプリ側の定数として解決する。

フィールド属性（10.4末尾の「プレイヤーの意思を薄く反映する」移動先抽選ロジック）に対応するカラムは、当該仕様が将来のアップデート項目として保留されているため持たせない。

### 40. chido_field_rarity_rate_master — フィールド別レアリティ抽選率

```sql
CREATE TABLE chido_field_rarity_rate_master (
    field_key   VARCHAR(64)       NOT NULL,               -- chido_field_master.field_key を参照
    rarity      TINYINT UNSIGNED  NOT NULL,               -- レアリティ（0: Common, 1: Uncommon, 2: Rare, 3: Mythic）。
                                                          -- Hidden(4) はイベント専用であり通常抽選の対象に一切含まれないため、行として存在させない
    rarity_rate SMALLINT UNSIGNED NOT NULL,               -- 抽選率。permyriad。同一 field_key 内の合計が 10000 になる（残差は存在しない＝必ず1つ選ばれる）
    PRIMARY KEY (field_key, rarity)
);
```

敵の抽選1段目。合計10000を前提とする確率値であるため、`weight`（相対重み）ではなく`_rate`として表現する。

### 41. chido_field_transition_master — フィールド遷移先候補

```sql
CREATE TABLE chido_field_transition_master (
    field_key      VARCHAR(64) NOT NULL, -- chido_field_master.field_key を参照。遷移元
    next_field_key VARCHAR(64) NOT NULL, -- chido_field_master.field_key を参照。遷移先候補
    PRIMARY KEY (field_key, next_field_key)
);
```

移動先は候補リストから完全ランダムで抽選するため、重み列を持たない。

**自己参照行と縮退の区別（B-11・決定事項）**: `(field_key, next_field_key)` に `(草原, 草原)` のような**自己参照行**を置くと「そこから動かない」がデータ上の意図として明示される（意図的な行き止まり）。一方、あるフィールドを遷移元とする行が**1件も存在しない**場合は、マスタ不整合とみなし切替時に草原へフォールバックする（戦闘システム 10.4参照）。この2つを区別できるのが自己参照行を許す理由である。

### 42. chido_enemy_group_master — 敵の組マスタ

```sql
CREATE TABLE chido_enemy_group_master (
    group_key VARCHAR(64)      NOT NULL PRIMARY KEY, -- 可読キー（例: 'slime_x3'）
    rarity    TINYINT UNSIGNED NOT NULL              -- 組のレアリティ。chido_enemy_master.rarity と共通のenum。
                                                     -- 敵の出現抽選（10.3）およびEscape時の再抽選例外の判定は、個体ではなく組のレアリティで行う
);
```

敵の出現抽選の単位は個体ではなく**組**である。レアリティ→敵1体という抽選方式では複数体の同時出現を表現できないため、レアリティ→組という2段階に改めた。単体の敵は「メンバーが1体の組」として表現する。

組をフィールドに従属させず独立した可読キーで定義するのは、同一の組を複数フィールドで再利用可能にするため（既存の`enemy_key`／`skill_key`等と同じ可読キー規約に従う）。

### 43. chido_enemy_group_member_master — 組の構成メンバー

```sql
CREATE TABLE chido_enemy_group_member_master (
    group_key    VARCHAR(64)      NOT NULL, -- chido_enemy_group_master.group_key を参照
    member_index TINYINT UNSIGNED NOT NULL, -- 出現順。chido_channel_current_enemy.spawn_index に引き継がれ、
                                            -- Discord埋め込みでの表示順、ひいてはターゲット自動再選定における「先頭の敵」を決定する
    enemy_key    VARCHAR(64)      NOT NULL, -- chido_enemy_master.enemy_key を参照
    PRIMARY KEY (group_key, member_index)
);
```

組の全メンバーは、出現時の`chido_channel_state.cumulative_enemy_level`と同一のレベルを持つ。メンバーごとのレベル差は設けない（強さの差は`chido_enemy_master.*_shape`と`strength_rate`で表現する）。

### 44. chido_field_enemy_group_master — フィールドに出現する組

```sql
CREATE TABLE chido_field_enemy_group_master (
    field_key VARCHAR(64)      NOT NULL,               -- chido_field_master.field_key を参照
    group_key VARCHAR(64)      NOT NULL,               -- chido_enemy_group_master.group_key を参照
    rarity    TINYINT UNSIGNED NOT NULL,               -- chido_enemy_group_master.rarity の非正規化キャッシュ。
                                                       -- 「フィールドF・レアリティRの組」を単一インデックスで引くために複製する。
                                                       -- 真実の情報源は chido_enemy_group_master 側であり、整合性の維持はアプリ側の責務
    PRIMARY KEY (field_key, group_key),
    INDEX idx_field_rarity (field_key, rarity)         -- 抽選2段目（レアリティ確定後の組の完全ランダム抽選）に使用
);
```

**草原 `Common` の行は必須（B-10・決定事項）**: 抽選レアリティに該当する組が現在フィールドに0件の場合、草原の `Common` の組へフォールバックする（戦闘システム 10.3参照）。このフォールバック先として、`field_key = 草原` かつ `rarity = Common` の行が1件以上必ず存在しなければならない。起動時（またはマスタ投入時）に検証し、0件なら起動を止める（戦闘システム 10.5参照）。フィールド遷移の草原フォールバック（41番）と合わせ、草原がシステム全体の最終防衛線になる。

### 45. chido_effect_element_grant_master — 状態変化：一時的な属性付与

```sql
CREATE TABLE chido_effect_element_grant_master (
    effect_key VARCHAR(64)  NOT NULL PRIMARY KEY, -- chido_effect_master.effect_key を参照
    elements   INT UNSIGNED NOT NULL              -- 付与する属性（ビット列）。
                                                  -- ダメージ計算時、対象の実効属性は「本体属性 ∪ 装備属性 ∪ 一時付与属性」として集計される
);
```

一時的な属性付与は、既存の3種（StatusModifier / SlipDamage / DisableMove）のいずれとも性質が異なる（%補正ではなくビット加算のため）。既存のステータス変動用テーブルに混在させず、専用のマスタとして新設する。

後天的・一時的な属性付与には保持数の上限を設けない（本体属性・スキル属性・モーション属性が「最大2つまでを目安」とするのとは異なる）。

対応するインスタンス側テーブルの要否は未確定（未確定事項 H-3 参照）。付与属性がマスタ固定であればインスタンス側は不要であり、`chido_effect_disable_move_master`と同じ扱いになる。

---

## 敵の出現・抽選ロジック（スキーマ外の運用注記）

### 戦闘チャンネル初期化コマンドの処理順序

```
1. chido_channel_state を INSERT
     channel_id             = 実行チャンネル
     current_field_key      = '草原'（アプリ側の定数）
     cumulative_enemy_level = 1
     current_session_id     = NULL
   → PK 重複で失敗した場合、そのチャンネルは初期化済みとしてエラーを返す

2. current_field_key から chido_field_rarity_rate_master でレアリティを重み付き抽選

3. (field_key, rarity) から chido_field_enemy_group_master で組を完全ランダム抽選

4. chido_enemy_group_member_master のメンバー分だけ chido_battle_enemy を INSERT（level = cumulative_enemy_level）

5. chido_channel_current_enemy に member_index を spawn_index として INSERT
```

セッションはここでは生成されない（プレイヤーの最初の戦闘行為時に生成される）。

ステップ2〜5は、通常の戦闘終了（`PlayerVictory`）時に呼ばれる次の敵の抽選と同一のロジックである。したがって戦闘システムドキュメント 10.3 の「このロジックが呼ばれるタイミング」は、「`PlayerVictory`による通常終了時」に加えて「戦闘チャンネル初期化時」を含むよう拡張が必要。

実行権限の制限は現時点では設けない。

### `end_reason` による次の敵の分岐

| `end_reason` | 累積敵レベル | 次の敵 |
|---|---|---|
| `PlayerVictory` | +1 | 通常抽選（レアリティ → 組） |
| `PlayerEscaped` | 変化なし | 逃走した**組**の`rarity`が`Rare`/`Mythic`/`Hidden`のいずれかであれば`Common`から再抽選。`Common`/`Uncommon`であれば同一の組を再出現させる |
| `EnemyEscaped` | 変化なし | レアリティ分岐を適用せず、**常に`Common`から再抽選** |
| `ChannelMissing` | — | `chido_channel_state`・`chido_channel_current_enemy`ごと削除 |

レベル上昇は組全体の撃破に対して +1 であり、メンバー1体につき +1 ではない。

### セッション終了トリガー

| トリガー | 判定 | `end_reason` |
|---|---|---|
| 敵側の生存が0（撃破） | `entity_type=1`の全行が`Defeated` | `PlayerVictory` |
| 敵側の生存が0（逃走を含む） | `entity_type=1`の全行が`Escaped`または`Defeated`で、最後の1体の消失原因が`Escaped` | `EnemyEscaped` |
| プレイヤー側の生存が0 | `entity_type=0`の全行が`Escaped` | `PlayerEscaped` |
| チャンネル消失 | Discordイベントの能動検知、または低頻度のバックグラウンド検証 | `ChannelMissing` |

「戦闘不能」であること自体は終了トリガーに含めない。全プレイヤーが`Defeated`であってもセッションは終了せず、新規参加者が仕留めに来るか、全員が改めてEscapeを選択するまで残り続ける。

---

## 未確定事項

今後詰めていく項目を、実際の動作への影響から「必須・推奨・任意」の3段階に分類する。今後の拡張方針として先送りする項目は「将来検討」（後述）に分離し、この優先度の枠組みからは除外する。

**項目IDは従来のものを維持する。第2次改訂で解消した項目のIDは詰め直さず、欠番のまま残す**（変更量が多く、各テーブルコメントが番号を直接引いているため、追跡コストが高い）。新規項目には `J-` を用いる。

### 必須 — データ破損・整合性の崩壊、または確定機能がスキーマ上未定義

**現時点でなし。** 第2次改訂により E-6・G-2 が、第3次改訂により下表の各項目（本改訂で確定しスキーマへ反映済み）が解消した。

| # | 項目 | 措置 |
|---|---|---|
| ~~E-6~~ | 組の複数メンバーを同時投入したときの表示順・ターゲット再選定順の根拠 | **解消**。4番に `display_order` を追加。`joined_at` から表示順の責務を剥奪した |
| ~~G-2~~ | 状態変化の持続の初期値の出所 | **解消**。10b・14番の `duration_actions`（付与元ごとに可変）。19番の型は `SMALLINT UNSIGNED`、NULL = 無期限 |
| ~~J-7~~ | `SlipDamage` の `attack_type` の供給源がスキーマ上に存在しなかった（22番はインスタンスに列を持つが、どのレイヤーも値を与えていなかった） | **解消**。10b・14番に `attack_type`（NULL許容、SlipDamage成分を持つ付与のみNOT NULL）を追加。付与時に22番へ複製。22番のコメントを「静的な性質の複製」に訂正（戦闘システム 5.4） |
| ~~J-8~~ | レベル算出式がスキーマ上に定義されていなかった（`level = √exp` が根拠としてのみ登場） | **解消**。戦闘システム 2.3 に `level = max(1, floor(√exp))` を一次仕様として明記。2番 `exp` 初期値1と併記。BigInteger整数平方根（浮動小数点非経由） |
| ~~J-9~~ | ダメージ軽減（防御）の表現がスキーマ上に定義されていなかった | **解消**。16番の `target_status` にDRRを編入（新規サブテーブルを設けない）。Defendは `fixed_rate=5000` の固定値行。合成は加算・下限1（戦闘システム 5.1） |
| ~~J-10~~ | 行動優先度（`Priority`）がスキーマ上に存在しなかった（Defendが行動順により無意味化する経路） | **解消**。9番に `priority INT NOT NULL DEFAULT 0` を追加。行動順 `priority → Speed → Random`（戦闘システム 4.1） |
| ~~J-11~~ | 敵の初期TP・ローテーション位置がスキーマ上に存在しなかった | **解消**。11番に `initial_tp`、4番に `rotation_index` を追加（戦闘システム 4.2・4.4） |

> この節は空だが削除しない。「必須事項は洗い出したうえで全て解消済み」という状態と、「そもそも洗い出していない」という状態を区別するため。

### 推奨 — 実害はないが、改善の余地がある

**J-3 / J-4 は、この中でも優先度が高い。** データ構造（スキルデータの作り方、または `chido_enemy_master` への列追加）に波及するため、マスタデータの作成を始める前に決まっている必要がある。

| # | 項目 | 波及先 |
|---|---|---|
| **G-1** | 参加者ごとの累積与ダメージの保持場所。`chido_battle_log.payload`(JSON)からの集計とするか、`chido_battle_participant`に専用列（例: `total_damage_dealt VARCHAR(100)`）を持たせるか。**実装は既に専用列（`chido_battle_participant.total_damage_dealt VARCHAR(100)`）を持っており、後者に倒れている。** 本項目を正式に解消とするかは戦闘システム側の判断待ち | 戦闘システム 6.2 で確定した経験値按分式の入力。4番の列構成 |
| **H-3** | `chido_effect_element_grant_instance`の要否。インスタンス側が可変値を持たないのであれば、`chido_effect_disable_move_master`と同じくマスタのみで完結する | 新規テーブルの要否 |
| **I-3** | `chido_battle_log`のログ粒度。1行=1ターンか、1行=1モーションか（1スキルが複数モーションを持つため） | G-1の解決方法によっては先に決める必要がある |
| ~~**I-4**~~ | **解消**。`last_action_at` は列ごと削除した。あわせて `message_id` も削除している（戦闘システム B-3・B-4 により、編集し続ける単一の進捗メッセージが存在しなくなったため）。3番のDDLと注記を参照 |

**解消した推奨項目（第3次改訂）**

| # | 項目 | 措置 |
|---|---|---|
| ~~J-3~~ 〔優先度：高〕 | 敵が `target_rule = 味方` のスキルを使う場合の対象解決規則 | **解消**。11番に `ally_target_rule TINYINT UNSIGNED NOT NULL DEFAULT 0` を追加（種族単位、`action_pattern_type` と対をなす）。候補集合は自分を含む。現行は 0=完全ランダム / 1=自分以外からランダム / 24=最小HP割合 の3規則を実装し、固定対象系・他情報参照系は予約値として将来拡張（戦闘システム 4.2・11.2-5参照） |
| ~~J-4~~ 〔優先度：高〕 | `target_rule = 味方` に自分自身を含むか、`[対象]` 省略時の既定 | **解消**。「味方」は自分自身を含み、`[対象]` 省略時は行動者自身に解決する。`target_rule = 自分自身` は「指定があっても対象は変わらない」より強い規則として区別（スキーマ波及なし。戦闘システム 4.2参照） |
| ~~J-5~~ | `[対象]` に `Escaped` / `Defeated` を指定できるか | **解消**。`Escaped` は候補集合に存在せず指定不可、`Defeated` は指定可だが常に拒否（将来の蘇生スキルのための布石として非対称なメッセージ）。いずれも行動の不成立でありターン・TP・反撃・減衰を生じない（スキーマ波及なし。戦闘システム 4.2参照） |
| ~~J-1~~ | 10番テーブル群における `elements` の配置 | **解消**。属性は攻撃モーションのみが持つと確定し、`chido_skill_motion_attack_master`（10a）へ移設。親からは削除。回復・付与・解除・離脱は属性を持たない（戦闘システム 5.3・11.2-8／11.4-4参照） |
| ~~J-2~~ | 10番テーブル群のテーブル名 | **解消**。親を `chido_skill_motion_master`（単数形）に改称。攻撃 `chido_skill_motion_attack_master`（10a）・回復 `chido_skill_motion_heal_master`（10b）に分割し「HP干渉」の語彙問題は消失。付与 `effect`（10c）・解除 `dispel`（10d）は据え置き |
| ~~J-12~~ 〔優先度：高〕 | 命中判定の道連れ（`accuracy_gate_group`） | **解消**。親 `chido_skill_motion_master` に `accuracy_gate_group SMALLINT UNSIGNED NULL` を追加。同一 `skill_key` 内で同値の行が1グループを成し、`motion_index` 最小の先頭が効果適用に到達しなければ他メンバーをスキップ。NULL は単独判定で後方互換。行間関係のため整合性検証はアプリ側（戦闘システム 4.2・11.2-11参照） |

**解消した推奨項目（第2次改訂）**

| # | 項目 | 措置 |
|---|---|---|
| ~~H-2~~ | 属性の10×10相性表の保持場所・内容 | **解消**。相性表はC#定数として保持し、`chido_element_affinity_master` は**新設しない**（戦闘システム 5.3参照）。属性の定義が `[Flags] enum Element` としてC#側にある以上、DB化すると二重管理になり、かつ属性追加時はどのみち再コンパイルが必要なため「再デプロイなしに変更できる」という唯一の実利が得られない |
| ~~I-5~~ | 回復モーションの`power`が何に対する%か | **解消**。術者の攻撃力基準（`回復量 = 攻撃力 × 威力`）。対象の防御力は影響しない（戦闘システム 5.1参照） |

### 任意 — 好み・スタイルに依存する

| # | 項目 | 備考 |
|---|---|---|
| **G-3** | Attack・Defendを`chido_player_skill`の習得管理対象外として扱う方式。`chido_skill_master`にフラグ列を設けるか、アプリ側の定数で`skill_key`を決め打ちするか。**本改訂で位置づけが変化した**: Attack/Defendの`skill_key`は習得管理除外に加え、TP+100契機（戦闘システム 4.4）と`priority`既定値（同 4.1）からも参照される共通定数になったため、単なる様式差ではなく「3つの参照点が同じ1箇所を指すための集約」になった。アプリ側定数として1箇所に集約するのが素直（9番の列構成に影響するが動作差はない） |

### 初回抽出（実装者視点の曖昧さ）由来のスキーマ波及項目

戦闘システムドキュメント 11.6 で洗い出した未着手項目のうち、DBスキーマに波及しうるものを再掲する（優先度は同 11.6 の分類に従う）。動作仕様の決定は戦闘システム側を正とし、本表は波及先の対応のみを示す。

| # | 優先度 | 項目 | スキーマ波及・措置 |
|---|---|---|---|
| ~~A-7~~ | 必須 | `DisableMove` 判定の内容 | **解消**。18番 `disable_rate` は「行動しようとするたびに引く確率」と確定。減衰は関与者集合（行動者＋反撃者）に従い19番・20番 `remaining_actions` を更新（コメント追記のみ。戦闘システム 5.4参照） |
| ~~A-9~~ | 必須 | `CurrentTarget` 未設定時（初回）の既定値 | **解消**。4番 `current_target_id` は初回既定・再選定を区別しない単一の導出関数で解決し、後段に落ちた結果を書き戻す。スキーマ変更なし（戦闘システム 3.3参照） |
| ~~A-10~~ | 推奨 | 敵側の `CurrentTarget` の設定・更新規則 | **解消**。敵は `current_target_id` を持たない（4番の「Enemy: 常にNULL」に寄せる）。敵の味方対象解決は 11番 `ally_target_rule` が別系統で担う。スキーマ変更なし |
| ~~B-9~~ | 必須 | `PlayerEscaped`/`EnemyEscaped` 時の再抽選と再生成 | **解消**。38番 `chido_channel_current_enemy` の書き込みは常に新規インスタンス（HP全快・装備再抽選・状態変化再適用）。引き継ぎ経路なし（戦闘システム 10.3参照） |
| ~~B-11~~ | 必須 | フィールド切替の判定タイミング・遷移先0件の扱い | **解消**。切替は `PlayerVictory` の +1 後に判定し切替後フィールドから抽選。41番 `chido_field_transition_master` は自己参照行が意図的な行き止まりを表し、行が無い場合は縮退（草原へフォールバック）。コメント追記のみ（戦闘システム 10.3・10.4参照） |
| ~~B-10~~ | 推奨 | 抽選レアリティに組が0件の場合 | **解消**。44番 `chido_field_enemy_group_master` に草原 `Common` の行が必須（フォールバック先）。草原 `Common` も0件なら例外。起動時検証で担保（戦闘システム 10.3・10.5参照） |
| B-14 | 推奨 | 通貨報酬の算出規則・敵装備のドロップ確率の出所 | 32番 `chido_enemy_currency_master` の按分規則、敵装備ドロップの参照経路（`chido_enemy_loots_master` 経由か装備マスタ側の列か） |
| B-15 | 推奨 | Luck の定義域（負値・100%超の扱い） | 10.2 のドロップ判定。スキーマ変更なしの可能性大 |

---

## 将来検討

今後の拡張（アップデート）方針として先送りし、優先度の枠組みから除外する項目。すぐに実装するわけではないが、「今後これをする予定がある」ことを明示しておくことで、機能追加時の競合や実装の複雑化を避けるねらいがある。

### 未確定事項から移動したもの

| # | 項目 | 備考 |
|---|---|---|
| **H-1** | 装備限定スキルの参照経路。`chido_equipment_skill_master`（1装備に複数スキルを許容）とするか、`chido_equipment_master`に単一の`skill_key`列を持たせるか | 装備限定スキル自体の実装とあわせて対応。新規テーブルまたは25番 |
| **J-6** | 「解除不可の状態変化」。将来必要になった場合、それはモーション側（10d）ではなく`chido_effect_master`側の列（例: `dispellable`）になる。「この効果は解除の対象にならない」は効果の性質であって、解除する側の性質ではないため | 15番への列追加。状態変化の寿命に関する不変条件があるため、解除不可の効果があっても暴走しない |
| **J-2v** | 10番テーブル群の読み取り用 VIEW。親＋子の LEFT JOIN で現行と同一の平坦な形が得られるが、これを VIEW として定義するか、C#側で JOIN を書くか。`CREATE VIEW` は既存テーブルに触れず、いつ追加・削除しても何も壊れないため、実際にデータを手入力して煩雑だと感じた時点で追加すればよい | 新規VIEWの要否（読み取り専用。MySQLでは複数テーブルにまたがるVIEWへのINSERTはできない） |

**解消した将来検討項目**

| # | 項目 | 措置 |
|---|---|---|
| ~~I-1~~ | `chido_player_effect`への`remaining_turns`適用可否 | **解消**。**適用する**。20番に `remaining_actions SMALLINT UNSIGNED NOT NULL` を追加。永続スコープの状態変化は「戦闘を跨ぎつつ有限」であり、これが `clear_on_battle_end` というフラグの存在理由そのものだった |

### 設計に余地を残している箇所（拡張予定の明示）

現時点では実装しないが、各テーブルが将来の拡張を見込んで設計上の余地を確保している箇所。追加実装時の競合を避けるため、ここに集約して明示する。

- **通貨単位の追加**（31番 `chido_player_currency` / 32番 `chido_enemy_currency_master`）: 新たな通貨種別を列追加で対応できる構成
- **アクセサリ枠の複数化**（27番 `chido_player_equipment_slot` / 30番 `chido_battle_enemy_equipment_slot`）: 番号付きスロットにより、両テーブル同時の列追加で対応
- **装備の個体差・強化**（26番 `chido_player_equipment` / 29番 `chido_battle_enemy_equipment`）: 装備インスタンスがGUIDを持つため、共有サブテーブルの追加で個体差を付与できる
- **フィールド属性による移動先抽選の「プレイヤーの意思反映」**（39番 `chido_field_master`）: 現状はフィールドに属性列を持たせていないが、将来解禁しうる
- **`target_status`のSpeed・Luckへの拡張**（16番 `chido_effect_status_modifier_master`）: 現時点はHP/攻撃/防御のみだが、決め打ちしない設計を維持する
- **`special_process_key`の実体**（横断的な設計方針「列挙値・ビット列・符号の使い分け」参照）: 一点物の特殊処理が必要になった段階で、個別のテーブル・実装を用意する
- **`affect_reason` の拡張**（19・20番）: 現状は `skill` / `auto` の2値だが、enumとして拡張可能な型で保持している。`grant_source_key` が「何のキーであるか」を示す型タグであり、`grant_source_key` の値からは導出できないため、将来 `equipment` 等が追加されても判別子として機能する

### 未確定事項ではない検討項目

- **10番テーブル群のマイグレーション方式**: これは設計上の未確定事項ではなく事実確認である。DBの構築自体がまだ行われていないため、テーブル定義を書き直す以外の作業は生じない。実装着手時のチェック項目として扱う

---

## 戦闘システム設計ドキュメントとの同期状況

### 第1次改訂ぶんの要追記事項（すべて反映済み）

初回改訂時に列挙していた22項目（8.3の全面書き換え、単一セッション制約、初期化コマンド、`ParticipantStatus`の全参加者適用、終了トリガーの3系統化、`BattleEndReason`の4値化、ロックの正準順序、抽選単位の「組」化ほか）は、`chido-battle-system-design.md` 側の整合改訂においてすべて反映済みである。同ドキュメントの章立ても再編されているため、本リストは廃止する。

### 第2次改訂における同期（本改訂で反映済み）

| 戦闘システムドキュメント側 | 内容 | 対応するDB側 |
|---|---|---|
| 2.3 | レイヤー内は加算・レイヤー間は乗算。加算合成の帰結としてDEFが負値を取りうる | 横断的な設計方針「補正値の合成」 |
| 2.4 | `elements = 0` の相性計算スキップは仕様分岐ではなく実装上の最適化 | — |
| 3.1 | 埋め込みにおける状態変化の表示（形式・集約・表示順・上限・Discordの文字数予算） | 4番 `display_order` / 19番・20番 `remaining_actions` |
| 3.3 | ターゲット再選定順の根拠を `display_order` に確定。`joined_at` は順序付けに使わない | 4番（E-6の解消） |
| 4.2 | モーション種別に `4: 状態変化解除` を追加。対象の解決規則（`target_rule`）をモーション単位に移設 | 9番（`range_type` 削除）／10番（`target_rule` 追加） |
| 4.2 | 戦闘離脱モーションは失敗しうる。ショートサーキットは離脱成功時のみ。反撃の有無は既存の処理順から自動的に導かれる | 10番 `accuracy_rate` |
| 4.4 | 回復モーションを含むスキルの `require_tp` は200以上 | 9番 `require_tp` |
| 5.1 | 防御貫通率の廃止。回復量 = 攻撃力 × 威力。`power` は Time To Kill 尺度の一次元量。勝敗の分水嶺 `E ≤ L` | 10a `power` / `attack_type`（I-5の解消） |
| 5.2 | クリティカルは回復量に適用しない | — |
| 5.3 | 10×10相性表の確定。補正式 `1.3^x` の単一化。C#定数として保持 | H-2の解消（`chido_element_affinity_master` を新設しない） |
| 5.4 | 「残りターン数」→「残り有効行動数」。持続の出所は付与元ごとに可変。NULL = 無期限。`clear_on_battle_end = 0` なら NOT NULL 必須 | 10b・14番 `duration_actions` ／ 19番・20番 `remaining_actions`（G-2・I-1の解消） |
| 5.4 | 重複付与時の挙動は「拒否」。判定キーはスコープごとに5値／4値。併存時の合算はレイヤー内加算 | 19番・20番 ／ 14番 `uk_enemy_effect` |
| 5.4 | 状態変化解除モーション（`motion_type = 4`）の新設 | 10c |
| 6.2 | 経験値按分式の確定。分母の下限。オーバーキルの扱いを「貢献率のクランプ」から「台帳に積む値の定義」へ変更 | 5番 `payload` / `actor_id`（G-1は未解決のまま） |
| 7.2 | `chido_player_effect` への書き込みはチャンネル行②に包摂される（①では不足） | 横断的な設計方針「排他制御とロックの正準順序」 |
| 9.1 | `/status` に永続スコープの状態変化を含める。分離基準は「セッションに属するか否か」 | 20番 |
| 11章 | 未確定事項の再編。**必須は0件** | 本ドキュメントの未確定事項 |

### 第3次改訂における同期（本改訂で反映済み）

初回抽出（実装者視点の曖昧さ）のうち A-3／A-4／A-5／B-2 系および A-1・A-11・B-2-f の確定内容を反映した。

| 戦闘システムドキュメント側 | 内容 | 対応するDB側 |
|---|---|---|
| 2.3 | レベル導出 `level = max(1, floor(√exp))`。BigInteger整数平方根（ニュートン法・浮動小数点非経由）。クランプは `Level` 取得時点。初期値1との二重防御 | 2番 `exp` 初期値1（J-8の解消） |
| 4.1 | 行動順を `priority → Speed → Random` に。乱数は1ターン1回で実行順とログ順を支配。Priority導入によりDefendがSpeed非依存で機能 | 9番 `priority`（J-10の解消） |
| 4.2 | Defendを全被ダメージへのDRR付与スキル（`target_rule=自分自身`・`duration_actions=1`）として再定義。敵スキル選択に `require_tp` フォールバック（`action_pattern_type` 別）。登録AttackとフォールバックAttackの区別 | 4番 `rotation_index` / 11番 `initial_tp` / 12番解説（J-11の解消） |
| 4.4 | TP契機をAttack/Defendモーションの再生に紐づけ。被攻撃TPの `被ダメージ` は実効ダメージ（台帳計上値）。`SlipDamage` 被弾でもインスタンス単位で蓄積。自滅TPも蓄積 | 4番 `current_tp` コメント更新 |
| 5.1 | パイプラインを攻撃・回復・スリップの3本に分割。PreDefenseは属性補正のみ（`13^x/10^x`）。全除算 `floor`・フェーズ境界で床る。DRR加算・下限1。Flatは恒等で残置。「回復＝被防御係数1の攻撃」は較正等価性へ読み替え | 16番 DRR（J-9の解消）／10a `power`・`attack_type` |
| 5.4 | `SlipDamage` の算出（攻撃同型・クリティカルなし・DRRなし）。スナップショットはATC・`attack_type` のみ。`attack_type` の出所は付与モーション。併存インスタンスは `instance_id` 順 | 10b・14番 `attack_type`（J-7の解消）／17番 `element`→`elements`／22番コメント訂正 |
| 5.4 | DRRを `StatusModifier` に編入（新規サブテーブルを設けない）。Defendは `fixed_rate=5000` | 16番 `target_status` にDRRを追加（J-9の解消） |
| 6.2 | 報酬ゲート＝台帳累計>0。実効ダメージ（台帳計上値）を与ダメージ帰属・被攻撃TP・報酬ゲートの共通基準量に | 5番 `payload`（G-1は未解決のまま） |
| 11.6 | 初回抽出の未着手項目を必須/推奨/任意に再分類 | 本ドキュメントの未確定事項「初回抽出由来のスキーマ波及項目」 |

### 相互に対応する未確定事項

| 戦闘システム側 | DB側 | 項目 |
|---|---|---|
| 11.2-1 | G-1 | 累積与ダメージの保持場所 |
| 11.2-3 | H-3 | `chido_effect_element_grant_instance` の要否 |
| 11.2-4 | I-3 | 戦闘ログの粒度 |
| — | I-4 | `last_action_at` の存否 |
| 11.2-5 | J-3 | 敵が「味方」対象スキルを使う場合の対象解決規則〔優先度：高〕 |
| 11.2-6 | J-4 | `target_rule = 味方` に自分自身を含むか、`[対象]` 省略時の既定〔優先度：高〕 |
| 11.2-7 | J-5 | `[対象]` に `Escaped` / `Defeated` を指定できるか |
| 11.2-8 | J-1 | 10番テーブル群における `elements` の配置 |
| 11.2-9 | J-2 | 10番テーブル群のテーブル名 |
| 11.3-1 | G-3 | Attack・Defend の習得管理除外方式 |
| 11.4-1 | H-1 | 装備限定スキルの参照経路 |
| 11.4-10 | J-6 | 解除不可の状態変化 |
| 11.4-11 | J-2v | 10番テーブル群の読み取り用 VIEW |
