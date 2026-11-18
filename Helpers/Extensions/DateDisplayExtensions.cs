using System.Globalization;

namespace StayBook.Helpers.Extensions;

/// <summary>Dates on server-rendered pages, written like the dashboard's <c>thaiDate</c>.</summary>
public static class DateDisplayExtensions
{
    private static readonly CultureInfo ThaiBuddhist = CreateThaiBuddhist();

    /// <summary><c>10 ม.ค. 2573</c> (Buddhist era) in Thai, <c>10 Jan 2030</c> otherwise.</summary>
    public static string ToDisplayDate(this DateOnly date) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th"
            ? date.ToString("d MMM yyyy", ThaiBuddhist)
            : date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static CultureInfo CreateThaiBuddhist()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("th-TH").Clone();
        culture.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();
        return culture;
    }
}
