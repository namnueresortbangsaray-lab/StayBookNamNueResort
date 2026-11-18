namespace StayBook.Models.Dtos;

/// <summary>
/// Guest feedback for bookings that start in the range (like the snapshot analytics): averages over the answers,
/// how many checked-out bookings answered, and the answers themselves, newest first.
/// </summary>
public sealed record FeedbackSummaryDto(
    DateOnly Start,
    DateOnly End,
    int CheckedOutCount,
    int ResponseCount,
    double AverageOverall,
    double AverageCleanliness,
    double AverageService,
    double AverageFacility,
    int WouldReturnCount,
    IReadOnlyList<FeedbackItemDto> Items);
