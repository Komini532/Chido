namespace Chido.Administration;

/// <summary>
/// 管理者コマンドの実行可否を、Discordユーザーidの許可リストで判定する。
/// ロール／権限ベースではなく「特定のユーザーのみ」という要件のため、
/// 環境変数 DISCORD_ADMIN_USER_IDS（カンマ区切りのDiscordユーザーID）で管理する。
/// 例: DISCORD_ADMIN_USER_IDS=111111111111111111,222222222222222222
/// </summary>
public static class AdminAuthorization
{
    private static readonly Lazy<HashSet<ulong>> AdminUserIds = new(ParseAdminUserIds);

    public static bool IsAuthorized(ulong userId) => AdminUserIds.Value.Contains(userId);

    private static HashSet<ulong> ParseAdminUserIds()
    {
        var raw = Environment.GetEnvironmentVariable("DISCORD_ADMIN_USER_IDS") ?? string.Empty;

        var ids = new HashSet<ulong>();
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ulong.TryParse(token, out var id))
                ids.Add(id);
            else
                Console.WriteLine($"[WARN] DISCORD_ADMIN_USER_IDS: '{token}' is not a valid Discord user id and was ignored.");
        }

        if (ids.Count == 0)
            Console.WriteLine("[WARN] DISCORD_ADMIN_USER_IDS is empty. No user can run admin commands.");

        return ids;
    }
}
