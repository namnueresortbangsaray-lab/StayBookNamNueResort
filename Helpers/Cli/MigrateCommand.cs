using Microsoft.EntityFrameworkCore;
using StayBook.Context;
using StayBook.Data.Seed;

namespace StayBook.Helpers.Cli;

public sealed class MigrateCommand(AppDbContext db) : ICliCommand
{
    public string Name => "migrate";

    public string Usage => "migrate";

    public async Task<int> RunAsync(CliArguments arguments, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        await db.Database.MigrateAsync(cancellationToken);
        await RoomCategorySync.AddMissingAsync(db, cancellationToken);
        await output.WriteLineAsync(pending.Count > 0 ? $"Applied: {string.Join(", ", pending)}" : "Database is current.");
        return 0;
    }
}
