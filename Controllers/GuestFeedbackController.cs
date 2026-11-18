using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayBook.Helpers.Http;
using StayBook.Models.ViewModels;
using StayBook.Services;

namespace StayBook.Controllers;

/// <summary>The guest feedback page (D-30): public, reached only through a sealed per-booking link.</summary>
[AllowAnonymous]
public sealed class GuestFeedbackController(GuestFeedbackService feedback) : Controller
{
    [HttpGet("/feedback/{token}")]
    [HttpHead("/feedback/{token}")]
    public async Task<IActionResult> Show(string token, CancellationToken cancellationToken) =>
        Page(await feedback.OpenAsync(token, cancellationToken));

    [HttpPost("/feedback/{token}")]
    [EnableRateLimiting(RateLimitPolicies.Guest)]
    public async Task<IActionResult> Submit(string token, CancellationToken cancellationToken)
    {
        var input = await RequestInput.ReadAsync(Request, cancellationToken);
        var model = await feedback.SubmitAsync(token, input, HttpContext.ClientIp(), cancellationToken);

        // Post/redirect/get: a reload shows the thank-you page instead of sending the form again.
        return model.State == GuestFeedbackState.Submitted ? Redirect($"{Request.PathBase}{Request.Path}{Request.QueryString}") : Page(model);
    }

    private ViewResult Page(GuestFeedbackViewModel model)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        var view = View("~/Views/GuestFeedback/Index.cshtml", model);
        view.StatusCode = model switch
        {
            { State: GuestFeedbackState.NotFound } => StatusCodes.Status404NotFound,
            { Errors.Count: > 0 } => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status200OK,
        };
        return view;
    }
}
