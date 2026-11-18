using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StayBook.Helpers.Options;
using StayBook.Models.Entities;
using StayBook.Models.ViewModels;
using StayBook.Services;
using StayBook.Services.Errors;

namespace StayBook.Tests.Data;

/// <summary>Guest feedback through a sealed per-booking link (D-30).</summary>
public sealed class GuestFeedbackTests(SqliteDatabaseFactory factory) : IClassFixture<SqliteDatabaseFactory>
{
    private const string Base = "https://stay.example";

    private static readonly (string Key, object? Value)[] Answer =
    [
        ("overall_rating", "5"), ("cleanliness_rating", "4"), ("service_rating", "5"), ("facility_rating", "3"),
        ("would_return", "yes"), ("comment", "  ห้องสะอาด\r\nพนักงานใจดี  "),
    ];

    private readonly EphemeralDataProtectionProvider protection = new();

    [Fact]
    public async Task LinkIsOnlyForCheckedOutBookings()
    {
        await using var s = await TestServices.CreateAsync(factory);
        var feedback = Service(s);
        var stay = await CreateStayAsync(s);

        var booked = await Assert.ThrowsAsync<ValidationException>(() => feedback.LinkAsync(stay.ToString(), Base, default));
        Assert.Equal("ส่งแบบประเมินให้ลูกค้าได้หลังเช็กเอาต์เท่านั้น", booked.Errors["status"]);
        await Assert.ThrowsAsync<NotFoundException>(() => feedback.LinkAsync(Guid.NewGuid().ToString(), Base, default));
        await Assert.ThrowsAsync<NotFoundException>(() => feedback.LinkAsync("not-an-id", Base, default));

        // A sealed id of a booking that is not checked out yet opens the "after check-out" page.
        var early = await feedback.OpenAsync(Seal(stay), default);
        Assert.Equal(GuestFeedbackState.NotReady, early.State);
    }

    [Fact]
    public async Task GuestAnswersOnceAndStaffSeeItInTheSummary()
    {
        await using var s = await TestServices.CreateAsync(factory);
        var feedback = Service(s);
        var stay = await CreateStayAsync(s);
        await CheckOutAsync(s, stay);

        var link = await feedback.LinkAsync(stay.ToString(), Base + "/", default);
        Assert.StartsWith($"{Base}/feedback/", link.Url, StringComparison.Ordinal);
        Assert.StartsWith("data:image/png;base64,", link.QrPng, StringComparison.Ordinal);
        Assert.False(link.Submitted);
        var token = link.Url[(link.Url.LastIndexOf('/') + 1)..];

        var page = await feedback.OpenAsync(token, default);
        Assert.Equal(GuestFeedbackState.Open, page.State);
        Assert.Equal("1", page.RoomName);

        var incomplete = await feedback.SubmitAsync(token, TestInputs.Input(("overall_rating", "6"), ("comment", new string('ก', 1001))), "203.0.113.9", default);
        Assert.Equal(GuestFeedbackState.Open, incomplete.State);
        Assert.Equal(
            ["cleanliness_rating", "comment", "facility_rating", "overall_rating", "service_rating", "would_return"],
            incomplete.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(await s.Db.Feedbacks.ToListAsync());

        var sent = await feedback.SubmitAsync(token, TestInputs.Input(Answer), "203.0.113.9", default);
        Assert.Equal(GuestFeedbackState.Submitted, sent.State);
        var row = await s.Db.Feedbacks.AsNoTracking().SingleAsync();
        Assert.Equal((5, 4, 5, 3, true), (row.OverallRating, row.CleanlinessRating, row.ServiceRating, row.FacilityRating, row.WouldReturn));
        Assert.Equal("ห้องสะอาด\nพนักงานใจดี", row.Comment);

        var audit = await s.Db.AuditLogs.AsNoTracking().SingleAsync(a => a.Action == "feedback");
        Assert.Equal(("stay", stay, null, "203.0.113.9"), (audit.EntityType, audit.EntityId, audit.UserId, audit.IpAddress));

        // A second answer is ignored and the link now shows the thank-you page.
        var again = await feedback.SubmitAsync(token, TestInputs.Input(("overall_rating", "1"), ("cleanliness_rating", "1"), ("service_rating", "1"), ("facility_rating", "1"), ("would_return", "no")), "203.0.113.9", default);
        Assert.Equal(GuestFeedbackState.Submitted, again.State);
        Assert.Equal(5, (await s.Db.Feedbacks.AsNoTracking().SingleAsync()).OverallRating);
        Assert.True((await feedback.LinkAsync(stay.ToString(), Base, default)).Submitted);

        Assert.Equal(5, (await s.Queries.StayAsync(stay))!.FeedbackRating);
        var summary = await feedback.SummaryAsync(new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31), default);
        Assert.Equal((1, 1, 5.0, 4.0, 1), (summary.CheckedOutCount, summary.ResponseCount, summary.AverageOverall, summary.AverageCleanliness, summary.WouldReturnCount));
        Assert.Equal("ลูกค้า ทดสอบ", Assert.Single(summary.Items).GuestName);
        Assert.Equal(0, (await feedback.SummaryAsync(new DateOnly(2030, 2, 1), new DateOnly(2030, 2, 28), default)).ResponseCount);
    }

    [Fact]
    public async Task UnknownOrAlteredLinksAreNotFound()
    {
        await using var s = await TestServices.CreateAsync(factory);
        var feedback = Service(s);
        var stay = await CreateStayAsync(s);
        await CheckOutAsync(s, stay);
        var token = Seal(stay);

        Assert.Equal(GuestFeedbackState.NotFound, (await feedback.OpenAsync("made-up", default)).State);
        Assert.Equal(GuestFeedbackState.NotFound, (await feedback.OpenAsync(token[..^2], default)).State);
        Assert.Equal(GuestFeedbackState.NotFound, (await feedback.OpenAsync(new EphemeralDataProtectionProvider().CreateProtector("StayBook.GuestFeedback.v1").Protect(stay.ToString()), default)).State);
        Assert.Equal(GuestFeedbackState.NotFound, (await feedback.SubmitAsync("made-up", TestInputs.Input(Answer), "203.0.113.9", default)).State);
        Assert.Equal(GuestFeedbackState.Open, (await feedback.OpenAsync(token, default)).State);
    }

    [Fact]
    public async Task LinksCloseAfterTheAnswerPeriod()
    {
        await using var s = await TestServices.CreateAsync(factory);
        var feedback = Service(s, linkDays: 30);
        var stay = await CreateStayAsync(s);
        await CheckOutAsync(s, stay);
        await s.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE stays SET check_in_date = '2020-01-01', check_out_date = '2020-01-02' WHERE id = {stay.ToString()}");

        var page = await feedback.OpenAsync(Seal(stay), default);
        Assert.Equal((GuestFeedbackState.Expired, new DateOnly(2020, 2, 1)), (page.State, page.ExpiresOn));
        Assert.Equal(GuestFeedbackState.Expired, (await feedback.SubmitAsync(Seal(stay), TestInputs.Input(Answer), "203.0.113.9", default)).State);
        var closed = await Assert.ThrowsAsync<ValidationException>(() => feedback.LinkAsync(stay.ToString(), Base, default));
        Assert.Contains("30 วัน", closed.Errors["status"], StringComparison.Ordinal);
        Assert.Empty(await s.Db.Feedbacks.ToListAsync());
    }

    private GuestFeedbackService Service(TestServices s, int linkDays = 30) => new(
        s.Db,
        protection,
        new AuditService(s.Db),
        new AppClock(TimeProvider.System, Microsoft.Extensions.Options.Options.Create(new AppOptions())),
        Microsoft.Extensions.Options.Options.Create(new FeedbackOptions { LinkDays = linkDays }));

    private string Seal(Guid stay) => protection.CreateProtector("StayBook.GuestFeedback.v1").Protect(stay.ToString("D"));

    private static async Task<Guid> CreateStayAsync(TestServices s)
    {
        var room = (await s.Queries.RoomsAsync())[0];
        return (await s.Stays.SaveAsync(TestInputs.Stay(room.Id, ("id_verified", true)), null, s.Admin, default)).Id;
    }

    private static async Task CheckOutAsync(TestServices s, Guid stay)
    {
        await s.Stays.CheckInAsync(stay.ToString(), s.Admin, default);
        await s.Stays.CheckoutAsync(stay.ToString(), TestInputs.Input(("no_damage", true)), s.Admin, default);
        Assert.Equal(StayStatus.CheckedOut, (await s.Queries.StayAsync(stay))!.Status);
        s.Db.ChangeTracker.Clear();
    }
}
