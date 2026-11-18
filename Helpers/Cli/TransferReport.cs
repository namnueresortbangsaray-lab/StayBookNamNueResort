using System.Globalization;
using StayBook.Data.Transfer;

namespace StayBook.Helpers.Cli;

public static class TransferReport
{
    /// <summary>One line per table: <c>name  source -> target</c> (same layout as the v1 command).</summary>
    public static async Task WriteAsync(TextWriter output, IEnumerable<TableCount> counts)
    {
        foreach (var count in counts)
        {
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{count.Table,-24} {count.Source} -> {count.Target}"));
        }
    }
}
