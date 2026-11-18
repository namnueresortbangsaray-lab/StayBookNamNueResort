using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StayBook.Context;
using StayBook.Data.Legacy;
using StayBook.Data.Transfer;
using StayBook.Helpers.Identity;

namespace StayBook.Tests.Data;

public sealed class LegacyImportTests(SqliteDatabaseFactory sqlite) : IClassFixture<SqliteDatabaseFactory>
{
    [Fact]
    public async Task ImportsLegacyDatabaseKeepingIdsAndPasswords()
    {
        var legacyPath = Path.Combine(Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.sqlite");
        await CreateLegacyDatabaseAsync(legacyPath);
        try
        {
            var data = await LegacyDatabaseReader.ReadAsync(legacyPath, default);
            await using var target = new AppDbContext(AppDbContextOptions.Create(await sqlite.CreateDatabaseAsync(), clock: null));
            await target.Database.MigrateAsync();
            var counts = await DataSetWriter.WriteAsync(target, data, truncate: true, dryRun: false, default);

            Assert.Equal(1, counts.Single(c => c.Table == "users").Target);
            Assert.Equal(1, counts.Single(c => c.Table == "stays").Target);
            Assert.Equal(1, counts.Single(c => c.Table == "attachments").Target);
            var user = await target.Users.SingleAsync();
            Assert.Equal(Guid.Parse(LegacyUserId), user.Id);
            Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, new LegacyBcryptPasswordHasher().VerifyHashedPassword(user, user.PasswordHash, "LegacyPassword123!"));
            var stay = await target.Stays.SingleAsync();
            Assert.Equal(new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero), stay.CreatedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(legacyPath);
        }
    }

    private const string LegacyUserId = "7c1d6f1e-2a4b-4c3d-8e9f-0a1b2c3d4e5f";

    /// <summary>A database as the v1 app created it (schema copied from the v1 release).</summary>
    private static async Task CreateLegacyDatabaseAsync(string path)
    {
        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v1_sqlite_schema.sql"));
        var hash = BCrypt.Net.BCrypt.HashPassword("LegacyPassword123!", 10).Replace("$2a$", "$2y$", StringComparison.Ordinal);
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = schema + $"""
            INSERT INTO users VALUES('{LegacyUserId}','ผู้ดูแลเดิม','legacy@staybook.local',$hash,'admin',1,'2026-10-01T01:00:00+00:00','2026-10-01T01:00:00+00:00',NULL);
            INSERT INTO rooms VALUES('11111111-1111-4111-8111-111111111111','1','อาคารหลัก','เตียงคู่',50000,2,3,10000,1,0,0,'2026-10-01T01:00:00+00:00','2026-10-01T01:00:00+00:00',NULL);
            INSERT INTO guests VALUES('22222222-2222-4222-8222-222222222222','สมชาย','081-234-5678','0812345678','ลูกค้าทั่วไป',0,NULL,NULL,'2026-10-01T02:00:00+00:00','2026-10-01T02:00:00+00:00',NULL);
            INSERT INTO stays VALUES('33333333-3333-4333-8333-333333333333','22222222-2222-4222-8222-222222222222','11111111-1111-4111-8111-111111111111','สมชาย','081-234-5678','2026-10-05','2026-10-07',2,0,100000,2,NULL,0,0,1,NULL,0,0,0,'2026-10-01T03:00:00+00:00','2026-10-01T03:00:00+00:00',NULL);
            INSERT INTO checkout_inspections VALUES('44444444-4444-4444-8444-444444444444','33333333-3333-4333-8333-333333333333',1,1,1,1,1,NULL,'{LegacyUserId}','2026-10-07T04:00:00+00:00');
            INSERT INTO feedbacks VALUES('55555555-5555-4555-8555-555555555555','33333333-3333-4333-8333-333333333333',5,5,4,5,1,'ดีมาก','2026-10-07T05:00:00+00:00','2026-10-07T05:00:00+00:00');
            INSERT INTO attachments VALUES('66666666-6666-4666-8666-666666666666','33333333-3333-4333-8333-333333333333','{LegacyUserId}','บัตร.jpg','{new string('a', 48)}.jpg','image/jpeg',3,'2026-10-05T06:00:00+00:00');
            INSERT INTO audit_logs VALUES('77777777-7777-4777-8777-777777777777','{LegacyUserId}','create','stay','33333333-3333-4333-8333-333333333333',NULL,'[1]','127.0.0.1','2026-10-01T03:00:00+00:00');
            """;
        command.Parameters.AddWithValue("$hash", hash);
        await command.ExecuteNonQueryAsync();
    }
}
