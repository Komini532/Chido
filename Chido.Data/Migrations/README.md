# マイグレーションの生成手順

`InitialCreate` は chido-database-design.md「確定スキーマ」章の全49テーブル
（採番1〜45番＋スキルモーションのサブタイプ 10a〜10d）に対応する。

## 生成コマンド

```bash
dotnet tool install --global dotnet-ef --version "8.*"

# 接続はしないが、ChidoDbContextFactory が接続文字列を要求するため設定が必要。
# ServerVersion は固定指定のため、実サーバーへ到達できなくてもよい
export CHIDO_MYSQL_CONNECTION="Server=localhost;Port=3306;Database=chido;User=x;Password=x;"

dotnet ef migrations add <Name> --project Chido.Data --startup-project Chido.Data --output-dir Migrations
```

生成したマイグレーションがモデルと同期しているかは次で確認できる。

```bash
dotnet ef migrations has-pending-model-changes --project Chido.Data --startup-project Chido.Data
```

DDL を目視で確認したい場合:

```bash
dotnet ef migrations script --project Chido.Data --startup-project Chido.Data -o initial.sql
```

## 注意点

- **`dotnet ef migrations remove` は実DBへの接続を要する**（適用済みかを問い合わせるため）。
  DBが無い環境で取り消す場合は、生成された3ファイル
  （`<timestamp>_<Name>.cs` / `.Designer.cs` / `ChidoDbContextModelSnapshot.cs`）を
  手で削除してから作り直す。
- net8.0 のランタイムが無い環境（新しい SDK のみが入っている場合）では、
  `DOTNET_ROLL_FORWARD=LatestMajor` を設定すると `dotnet-ef` が起動できる。
- **MySQL 8.4 では `mysql_native_password` プラグインが削除されている。** 接続ユーザーの
  認証方式は `caching_sha2_password`（8.0 以降のデフォルト）である必要がある。
  MySqlConnector 側は対応済みのため接続文字列に変更は要らないが、8.0 から引き継いだ
  ユーザーが `mysql_native_password` の場合は接続できない。その場合は
  `ALTER USER 'chido'@'%' IDENTIFIED WITH caching_sha2_password BY '<password>';` で切り替える。

## 実DBに対する適用の確認

MySQL 8.4.11（`STRICT_TRANS_TABLES`）に対して適用を確認済み。全49テーブルの作成、
`exp_len` / `amount_len` のストアド生成列、およびランキング用インデックスの逆走査
（`Backward index scan; Using index`。`filesort` なし）まで含めて動作する。
以前は 8.0.46 で確認していたが、8.0 系が 2026-04-30 にEOLとなったため 8.4 LTS で取り直した。
`ChidoDbContextFactory` の `MySqlServerVersion` を 8.0.36 から 8.4.0 に上げても、
EF Core が生成する DDL は1バイトも変わらない（Pomelo が機能の可否を切り替える閾値が
その区間に存在しないため）。マイグレーションの再生成は不要。

```bash
docker run -d --name chido-mysql -e MYSQL_ROOT_PASSWORD=chido -e MYSQL_DATABASE=chido \
  -p 13306:3306 mysql:8.4 --sql-mode="STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION"

export CHIDO_MYSQL_CONNECTION="Server=127.0.0.1;Port=13306;Database=chido;User=root;Password=chido;"
dotnet ef database update --project Chido.Data --startup-project Chido.Data
```

逆走査が効いているかは次で確認する。

```sql
EXPLAIN SELECT user_id FROM chido_battle_status ORDER BY exp_len DESC, exp DESC LIMIT 10;
-- Extra: Backward index scan; Using index （"Using filesort" が出ないこと）
```

## 実DBを要するテスト

上記の確認は `Chido.Data.Tests` の `DatabaseSchemaTests` が自動化している
（DDLの適用・全49テーブルの作成・生成列の算出・ランキングの数値順・逆走査の `EXPLAIN`）。
CI（`.github/workflows/build.yml`）は MySQL 8.4 のサービスコンテナを立てて毎回これを走らせる。

手元で走らせる場合は、テスト専用のDBを立てて `CHIDO_TEST_MYSQL_CONNECTION` を設定する。

```bash
docker run -d --name chido-mysql-test -e MYSQL_ROOT_PASSWORD=chido -e MYSQL_DATABASE=chido_test \
  -p 13306:3306 mysql:8.4 --sql-mode="STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION"

export CHIDO_TEST_MYSQL_CONNECTION="Server=127.0.0.1;Port=13306;Database=chido_test;User=root;Password=chido;"
dotnet test Chido.Data.Tests
```

- **接続文字列の変数は実行時用（`CHIDO_MYSQL_CONNECTION`）と分けている。** これらのテストは
  対象DBを破棄して作り直すため、本番・開発用のDBを指した状態で走ると中身が消える。
  データベース名が `_test` で終わらない場合は実行を拒否する二重の歯止めも入れている。
- **未設定の環境ではスキップされる。** Docker を用意せずに `dotnet test` を打っても落ちない。
  ただしCIでは `CHIDO_REQUIRE_DATABASE_TESTS=1` を立ててスキップを禁じており、
  サービスコンテナの設定が壊れた場合は緑にならずに失敗する。
- **サービスコンテナには `--sql-mode` を渡せない**（`services:` に起動コマンドを書く口が無い）。
  イメージ既定の `sql_mode` は `STRICT_TRANS_TABLES` を含むより厳しい組み合わせのため、
  ワークフロー側で `SET GLOBAL sql_mode` を実行して本番と同じ組み合わせへ揃えている。

実DBに接続しないテスト（`SchemaTests` / `RankingQueryTests`）も引き続き併存する。
EF Core が確定させたモデルと `ToQueryString()` を読む形で、DBを立てずに同じ不変条件を固定している。

排他制御（`SELECT ... FOR UPDATE`）の検証は引き続き Phase 6 で行う。
