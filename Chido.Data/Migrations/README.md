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

## 実DBに対する適用の確認

MySQL 8.0.46（`STRICT_TRANS_TABLES`）に対して適用を確認済み。全49テーブルの作成、
`exp_len` / `amount_len` のストアド生成列、およびランキング用インデックスの逆走査
（`Backward index scan; Using index`。`filesort` なし）まで含めて動作する。

```bash
docker run -d --name chido-mysql -e MYSQL_ROOT_PASSWORD=chido -e MYSQL_DATABASE=chido \
  -p 13306:3306 mysql:8.0 --sql-mode="STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION"

export CHIDO_MYSQL_CONNECTION="Server=127.0.0.1;Port=13306;Database=chido;User=root;Password=chido;"
dotnet ef database update --project Chido.Data --startup-project Chido.Data
```

CI（`.github/workflows/build.yml`）にはMySQLのサービスコンテナを置いていないため、
この確認は手元で行う。テスト（`SchemaTests` / `RankingQueryTests`）は実DBに接続せず、
EF Core が確定させたモデルと `ToQueryString()` を読む形で同じ内容を固定している。

排他制御（`SELECT ... FOR UPDATE`）の検証は引き続き Phase 6 で行う。
