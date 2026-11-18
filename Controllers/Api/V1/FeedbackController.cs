using Microsoft.AspNetCore.Mvc;
using StayBook.Helpers.Authorization;
using StayBook.Helpers.Http;
using StayBook.Services;

namespace StayBook.Controllers.Api.V1;

/// <summary>Guest feedback for staff (D-30): the link to hand a guest, and the answers for the reports page.</summary>
public sealed class FeedbackController(GuestFeedbackService feedback, SnapshotService snapshots) : ApiControllerBase
{
    /// <summary>Link and QR code for a checked-out booking (422 before check-out or after the answer period).</summary>
    [HttpGet("stays/{id}/feedback-link")]
    [RequireAbility(Abilities.Checkout)]
    public async Task<IActionResult> Link(string id, CancellationToken cancellationToken) =>
        ApiResults.Ok(await feedback.LinkAsync(id, $"{Request.Scheme}://{Request.Host}{Request.PathBase}", cancellationToken), "โหลดข้อมูลแล้ว");

    /// <summary>Answers for bookings that start in the range (validated like the snapshot; default: current month).</summary>
    [HttpGet("feedback")]
    public async Task<IActionResult> Summary([FromQuery] string? start, [FromQuery] string? end, CancellationToken cancellationToken)
    {
        var (from, to) = snapshots.ResolveRange(start, end);
        return ApiResults.Ok(await feedback.SummaryAsync(from, to, cancellationToken), "โหลดข้อมูลแล้ว");
    }
}
