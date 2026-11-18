using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace StayBook.Context;

/// <summary>SQLite-only functions for LINQ queries, registered in <see cref="AppDbContext"/>.</summary>
public static class SqliteFunctions
{
    /// <summary>
    /// The SQLite <c>rowid</c> of the row whose key column is passed (<c>SqliteFunctions.RowId(s.Id)</c> →
    /// <c>"s"."rowid"</c>). Tables keep their implicit rowid, which grows with insertion order, so it breaks ties
    /// between rows created in the same second (timestamps are stored with second precision). Read-only and not
    /// part of the model, so no migration is involved. Only usable inside a query.
    /// </summary>
    public static long RowId(Guid key) =>
        throw new InvalidOperationException($"{nameof(RowId)} can only be used in a LINQ to Entities query.");

    internal static void Register(ModelBuilder modelBuilder) =>
        modelBuilder.HasDbFunction(typeof(SqliteFunctions).GetMethod(nameof(RowId))!)
            .HasTranslation(args => args[0] is ColumnExpression column
                ? new ColumnExpression("rowid", column.TableAlias, typeof(long), new LongTypeMapping("INTEGER"), nullable: false)
                : throw new InvalidOperationException($"{nameof(RowId)} needs a column of the table, such as its key."));
}
