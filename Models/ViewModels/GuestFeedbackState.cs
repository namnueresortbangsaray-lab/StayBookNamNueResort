namespace StayBook.Models.ViewModels;

/// <summary>What the guest feedback page shows for a link.</summary>
public enum GuestFeedbackState
{
    /// <summary>Unknown or damaged link, or the booking was deleted (404).</summary>
    NotFound,

    /// <summary>The booking is not checked out yet.</summary>
    NotReady,

    /// <summary>More than <c>Feedback__LinkDays</c> days after check-out.</summary>
    Expired,

    /// <summary>The form.</summary>
    Open,

    /// <summary>Already answered: the thank-you message.</summary>
    Submitted,
}
