using Microsoft.EntityFrameworkCore.Design;

namespace Chido.Data;

/// <summary>
/// `dotnet ef migrations add` 等のCLIツールがDbContextを構築するためのファクトリ。
/// Program.cs側にDIコンテナ（Generic Host）が存在しない現状の構成でも、
/// アプリ本体を起動せず素直にマイグレーションを生成できるようにするためのもの。
/// </summary>
public sealed class ChidoDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ChidoDbContext>
{
    public ChidoDbContext CreateDbContext(string[] args)
        => ChidoDbContextFactory.CreateDbContext();
}
