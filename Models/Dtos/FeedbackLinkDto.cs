namespace StayBook.Models.Dtos;

/// <summary>The guest's feedback link for one booking, with a QR code (PNG data URI) to scan.</summary>
public sealed record FeedbackLinkDto(Guid StayId, string Url, string QrPng, DateOnly ExpiresOn, bool Submitted);
