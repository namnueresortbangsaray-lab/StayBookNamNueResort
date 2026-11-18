namespace ResortGuestbook.Tests.Data;

/// <summary>Creates an empty database per test.</summary>
public interface IDatabaseFactory
{
    Task<string> CreateDatabaseAsync();
}
