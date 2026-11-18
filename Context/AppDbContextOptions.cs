using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StayBook.Context.Interceptors;

namespace StayBook.Context;

/// <summary>The only place that configures EF Core: SQLite, its pragmas and the timestamp interceptor.</summary>
public static class AppDbContextOptions
{
    /// <summary>Configures EF Core for SQLite; a null <paramref name="clock"/> disables timestamp stamping (used when copying rows verbatim).</summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString, TimeProvider? clock)
    {
        EnsureSqliteDirectory(connectionString);
        builder.UseSqlite(connectionString);
        builder.AddInterceptors(new SqlitePragmaInterceptor());
        if (clock is not null)
        {
            builder.AddInterceptors(new TimestampInterceptor(clock));
        }

        return builder;
    }

    public static DbContextOptions<AppDbContext> Create(string connectionString, TimeProvider? clock)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        Configure(builder, connectionString, clock);
        return builder.Options;
    }

    private static void EnsureSqliteDirectory(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:" || dataSource.StartsWith("file:", StringComparison.Ordinal))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
