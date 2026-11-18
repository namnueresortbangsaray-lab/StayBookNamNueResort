namespace StayBook.Models.ViewModels;

/// <summary>
/// The guest feedback page. Holds no guest name or phone: the link may be forwarded, so it only names the room and
/// dates. <see cref="Errors"/> maps a field to a resource key, so the page can show it in Thai or English.
/// </summary>
public sealed record GuestFeedbackViewModel
{
    public required GuestFeedbackState State { get; init; }

    public string? RoomName { get; init; }

    public DateOnly? CheckInDate { get; init; }

    public DateOnly? CheckOutDate { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    /// <summary>Ratings entered so far (1–5), kept when the form comes back with errors.</summary>
    public IReadOnlyDictionary<string, int> Ratings { get; init; } = new Dictionary<string, int>();

    public bool? WouldReturn { get; init; }

    public string? Comment { get; init; }

    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();
}
