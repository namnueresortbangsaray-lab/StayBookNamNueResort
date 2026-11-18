using Microsoft.EntityFrameworkCore.Design;

namespace StayBook.Context;

/// <summary>Lets <c>dotnet ef</c> build the Sqlite model without a running host or database.</summary>
public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(AppDbContextOptions.Create("Data Source=:memory:", clock: null));
}
