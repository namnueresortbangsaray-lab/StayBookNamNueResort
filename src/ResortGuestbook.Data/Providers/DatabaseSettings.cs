using Microsoft.Extensions.Configuration;

namespace ResortGuestbook.Data.Providers;

/// <summary>Database configuration resolved from <c>Database:*</c> and <c>ConnectionStrings:Sqlite</c>.</summary>
public sealed record DatabaseSettings(string SqliteConnectionString, bool AutoMigrate, bool Seed)
{
    public const string DefaultSqliteConnectionString = "Data Source=storage/database/staybook.db";

    public static DatabaseSettings FromConfiguration(IConfiguration configuration)
    {
        // PostgreSQL/Supabase support was removed (D-28); fail loudly instead of silently using SQLite.
        if (configuration["Database:Provider"] is { Length: > 0 } provider && !provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Database provider '{provider}' is not supported; Stay Book uses SQLite only.");
        }

        return new(
            configuration.GetConnectionString("Sqlite") is { Length: > 0 } sqlite ? sqlite : DefaultSqliteConnectionString,
            configuration.GetValue("Database:AutoMigrate", true),
            configuration.GetValue("Database:Seed", false));
    }
}
