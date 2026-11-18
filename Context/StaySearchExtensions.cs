using Microsoft.EntityFrameworkCore;
using StayBook.Models.Entities;

namespace StayBook.Context;

/// <summary>
/// Case-insensitive substring search over guest name, phone, room name, zone and note, reproducing the v1
/// <c>Dialect::search</c> on SQLite: <c>LOWER(col) LIKE LOWER(:p)</c>.
/// The pattern is used as given (<c>%</c>/<c>_</c> are wildcards, as in v1).
/// </summary>
public static class StaySearchExtensions
{
    public static IQueryable<Stay> WhereTextMatches(this IQueryable<Stay> stays, string pattern) =>
        stays.Where(s =>
            EF.Functions.Like(s.GuestName.ToLower(), pattern.ToLower())
            || EF.Functions.Like(s.Phone!.ToLower(), pattern.ToLower())
            || EF.Functions.Like(s.Room!.Name.ToLower(), pattern.ToLower())
            || EF.Functions.Like(s.Room!.Zone.ToLower(), pattern.ToLower())
            || EF.Functions.Like(s.Note!.ToLower(), pattern.ToLower()));
}
