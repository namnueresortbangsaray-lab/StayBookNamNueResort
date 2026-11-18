namespace StayBook.Models.Dtos;

/// <summary>A <c>feedbacks</c> row (as v1 returned it); used for the audit entry of a guest's answer.</summary>
public sealed record FeedbackDto(
    Guid Id,
    Guid StayId,
    int CleanlinessRating,
    int ServiceRating,
    int FacilityRating,
    int OverallRating,
    bool WouldReturn,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
