using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace StayBook.Context;

public static class DbErrors
{
    private const int SqliteConstraint = 19;

    /// <summary>
    /// Mirrors the v1 check (<c>SQLSTATE 23000</c> or a message mentioning "unique"): any SQLite constraint failure
    /// means "this data already exists" (HTTP 409).
    /// </summary>
    public static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraint };
}
