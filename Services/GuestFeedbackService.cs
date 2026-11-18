using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using StayBook.Context;
using StayBook.Helpers.Http;
using StayBook.Helpers.Options;
using StayBook.Models.Dtos;
using StayBook.Models.Entities;
using StayBook.Models.ViewModels;
using StayBook.Services.Errors;

namespace StayBook.Services;

/// <summary>
/// Guest feedback (D-30): staff hand a checked-out guest a link (or its QR code); the guest rates the stay once,
/// without signing in, until <c>Feedback__LinkDays</c> after check-out. The link carries the booking id sealed with
/// ASP.NET Core Data Protection, so nothing is stored for it and it cannot be guessed or altered. Answers go into the
/// v1 <c>feedbacks</c> table, which staff no longer write (D-24).
/// </summary>
public sealed class GuestFeedbackService(
    AppDbContext db,
    IDataProtectionProvider protection,
    AuditService audit,
    AppClock clock,
    IOptions<FeedbackOptions> options)
{
    public const string BookingNotFound = "ไม่พบ Booking";
    public const int CommentMaxLength = 1000;

    /// <summary>Rating fields in the order the form asks them.</summary>
    public static readonly IReadOnlyList<string> RatingKeys = ["overall_rating", "cleanliness_rating", "service_rating", "facility_rating"];

    private readonly IDataProtector protector = protection.CreateProtector("StayBook.GuestFeedback.v1");

    /// <summary>The link and QR code for a checked-out booking; <paramref name="requestBase"/> is used without <c>Feedback__PublicBaseUrl</c>.</summary>
    public async Task<FeedbackLinkDto> LinkAsync(string id, string requestBase, CancellationToken cancellationToken)
    {
        var stay = Guid.TryParse(id, out var key)
            ? await db.Stays.AsNoTracking().Include(s => s.Feedback).FirstOrDefaultAsync(s => s.Id == key, cancellationToken)
            : null;
        if (stay is null)
        {
            throw new NotFoundException(BookingNotFound);
        }

        if (stay.Status != StayStatus.CheckedOut)
        {
            throw new ValidationException("status", "ส่งแบบประเมินให้ลูกค้าได้หลังเช็กเอาต์เท่านั้น");
        }

        var expiresOn = ExpiresOn(stay);
        if (stay.Feedback is null && clock.Today > expiresOn)
        {
            throw new ValidationException("status", $"หมดเวลาให้ลูกค้าประเมินแล้ว (ประเมินได้ภายใน {options.Value.LinkDays} วันหลังเช็กเอาต์)");
        }

        var baseUrl = string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl) ? requestBase : options.Value.PublicBaseUrl;
        var url = $"{baseUrl.TrimEnd('/')}/feedback/{protector.Protect(stay.Id.ToString("D"))}";
        return new FeedbackLinkDto(stay.Id, url, QrPng(url), expiresOn, stay.Feedback is not null);
    }

    /// <summary>The page for a link: the form, or why there is none.</summary>
    public async Task<GuestFeedbackViewModel> OpenAsync(string token, CancellationToken cancellationToken)
    {
        var (stay, state) = await ResolveAsync(token, cancellationToken);
        return Page(stay, state);
    }

    /// <summary>Saves the guest's answer, or returns the form with errors (field → resource key).</summary>
    public async Task<GuestFeedbackViewModel> SubmitAsync(string token, RequestInput input, string ip, CancellationToken cancellationToken)
    {
        var (stay, state) = await ResolveAsync(token, cancellationToken);
        if (stay is null || state != GuestFeedbackState.Open)
        {
            return Page(stay, state);
        }

        var errors = new Dictionary<string, string>();
        var ratings = new Dictionary<string, int>();
        foreach (var field in RatingKeys)
        {
            var value = Coerce.ToInt(input.Get(field, 0L));
            if (value is >= 1 and <= 5)
            {
                ratings[field] = (int)value;
            }
            else
            {
                errors[field] = "ui.guest_feedback.error.rating";
            }
        }

        bool? wouldReturn = input.Get("would_return", null) switch
        {
            bool answer => answer,
            string text when text.Trim().ToLowerInvariant() is "yes" or "1" or "true" => true,
            string text when text.Trim().ToLowerInvariant() is "no" or "0" or "false" => false,
            _ => null,
        };
        if (wouldReturn is null)
        {
            errors["would_return"] = "ui.guest_feedback.error.would_return";
        }

        var comment = CleanComment(input.Get("comment", null));
        if (comment?.Length > CommentMaxLength)
        {
            errors["comment"] = "ui.guest_feedback.error.comment";
        }

        if (errors.Count > 0)
        {
            return Page(stay, state) with { Ratings = ratings, WouldReturn = wouldReturn, Comment = comment, Errors = errors };
        }

        var feedback = new Feedback
        {
            Id = Guid.NewGuid(),
            StayId = stay.Id,
            OverallRating = ratings["overall_rating"],
            CleanlinessRating = ratings["cleanliness_rating"],
            ServiceRating = ratings["service_rating"],
            FacilityRating = ratings["facility_rating"],
            WouldReturn = wouldReturn!.Value,
            Comment = comment,
        };
        db.Feedbacks.Add(feedback);
        try
        {
            // AuditService saves the feedback row and its audit entry together.
            await audit.RecordGuestAsync("feedback", "stay", stay.Id, null, ToDto(feedback), ip, cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsDuplicate(exception))
        {
            // Sent twice at the same moment (double tap): the first answer stands.
            db.ChangeTracker.Clear();
        }

        return Page(stay, GuestFeedbackState.Submitted);
    }

    /// <summary>Answers for bookings whose check-in date is in the range, newest answer first.</summary>
    public async Task<FeedbackSummaryDto> SummaryAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var checkedOut = await db.Stays.AsNoTracking()
            .CountAsync(s => s.CheckInDate >= start && s.CheckInDate <= end && s.Status == StayStatus.CheckedOut, cancellationToken);
        var items = (await db.Stays.AsNoTracking()
            .Where(s => s.CheckInDate >= start && s.CheckInDate <= end && s.Feedback != null)
            .Select(s => new FeedbackItemDto(
                s.Id, s.GuestName, s.Room!.Name, s.CheckInDate, s.CheckOutDate,
                s.Feedback!.CleanlinessRating, s.Feedback.ServiceRating, s.Feedback.FacilityRating, s.Feedback.OverallRating,
                s.Feedback.WouldReturn, s.Feedback.Comment, s.Feedback.CreatedAt))
            .ToListAsync(cancellationToken))
            .OrderByDescending(f => f.CreatedAt)
            .ToList();
        double Average(Func<FeedbackItemDto, int> rating) => items.Count > 0 ? items.Average(rating) : 0;
        return new FeedbackSummaryDto(
            start,
            end,
            checkedOut,
            items.Count,
            Average(f => f.OverallRating),
            Average(f => f.CleanlinessRating),
            Average(f => f.ServiceRating),
            Average(f => f.FacilityRating),
            items.Count(f => f.WouldReturn),
            items);
    }

    public static FeedbackDto ToDto(Feedback f) => new(
        f.Id, f.StayId, f.CleanlinessRating, f.ServiceRating, f.FacilityRating, f.OverallRating, f.WouldReturn, f.Comment, f.CreatedAt, f.UpdatedAt);

    private async Task<(Stay? Stay, GuestFeedbackState State)> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        Guid? id = null;
        try
        {
            id = Guid.TryParse(protector.Unprotect(token), out var key) ? key : null;
        }
        catch (CryptographicException)
        {
            // Not one of our links (typo, truncated, or made up).
        }

        var stay = id is { } stayId
            ? await db.Stays.AsNoTracking().Include(s => s.Room).Include(s => s.Feedback).FirstOrDefaultAsync(s => s.Id == stayId, cancellationToken)
            : null;
        return stay switch
        {
            null => (null, GuestFeedbackState.NotFound),
            { Feedback: not null } => (stay, GuestFeedbackState.Submitted),
            { Status: not StayStatus.CheckedOut } => (stay, GuestFeedbackState.NotReady),
            _ when clock.Today > ExpiresOn(stay) => (stay, GuestFeedbackState.Expired),
            _ => (stay, GuestFeedbackState.Open),
        };
    }

    private GuestFeedbackViewModel Page(Stay? stay, GuestFeedbackState state) => stay is null || state == GuestFeedbackState.NotFound
        ? new GuestFeedbackViewModel { State = GuestFeedbackState.NotFound }
        : new GuestFeedbackViewModel
        {
            State = state,
            RoomName = stay.Room?.Name,
            CheckInDate = stay.CheckInDate,
            CheckOutDate = stay.CheckOutDate,
            ExpiresOn = ExpiresOn(stay),
        };

    private DateOnly ExpiresOn(Stay stay) => stay.CheckOutDate.AddDays(options.Value.LinkDays);

    private static string? CleanComment(object? value)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Line breaks stay; other control characters (including \r) go.
        var cleaned = new string([.. text.Where(c => !char.IsControl(c) || c is '\n')]).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static string QrPng(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        return $"data:image/png;base64,{Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(8))}";
    }
}
