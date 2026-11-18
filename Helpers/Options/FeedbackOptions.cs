namespace StayBook.Helpers.Options;

/// <summary>Guest feedback links (<c>Feedback__*</c>, deviation D-30).</summary>
public sealed class FeedbackOptions
{
    /// <summary>A guest can answer until this many days after the booking's check-out date.</summary>
    public int LinkDays { get; set; } = 30;

    /// <summary>
    /// Address guests open the link with, e.g. <c>https://staybook.example.com</c>. Empty: the address staff use, which
    /// only works for guests on the same network when the app runs on the resort LAN.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
