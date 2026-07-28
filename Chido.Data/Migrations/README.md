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
- 実DBに対する適用（`MigrateAsync()`）の確認はまだ行っていない。
  排他制御の検証で実DBが必要になる段階で併せて確認する。
