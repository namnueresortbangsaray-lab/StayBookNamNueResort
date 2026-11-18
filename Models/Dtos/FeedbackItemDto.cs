namespace StayBook.Models.Dtos;

/// <summary>One guest's answer with the booking it belongs to.</summary>
public sealed record FeedbackItemDto(
    Guid StayId,
    string GuestName,
    string RoomName,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int CleanlinessRating,
    int ServiceRating,
    int FacilityRating,
    int OverallRating,
    bool WouldReturn,
    string? Comment,
    DateTimeOffset CreatedAt);
