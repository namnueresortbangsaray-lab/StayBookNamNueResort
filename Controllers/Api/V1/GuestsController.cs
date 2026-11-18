using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayBook.Helpers.Authorization;
using StayBook.Helpers.Http;
using StayBook.Services;

namespace StayBook.Controllers.Api.V1;

[EnableRateLimiting(RateLimitPolicies.Writes)]
public sealed class GuestsController(GuestService guests) : ApiControllerBase
{
    [HttpPut("guests/{id}")]
    [RequireAbility(Abilities.EditGuest)]
    public async Task<IActionResult> Update(string id, CancellationToken cancellationToken) =>
        ApiResults.Ok(await guests.UpdateAsync(await InputAsync(), id, HttpContext.Actor(), cancellationToken), Saved);
}
