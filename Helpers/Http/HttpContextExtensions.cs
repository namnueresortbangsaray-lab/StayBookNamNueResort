using System.Security.Claims;
using StayBook.Helpers.Identity;
using StayBook.Services;

namespace StayBook.Helpers.Http;

public static class HttpContextExtensions
{
    /// <summary>The client IP as recorded by v1's <c>REMOTE_ADDR</c> (after trusted forwarded headers).</summary>
    public static string ClientIp(this HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    public static Actor Actor(this HttpContext context)
    {
        var user = context.User;
        return new Actor(
            Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Not signed in.")),
            user.FindFirstValue(AppClaimsPrincipalFactory.DisplayNameClaim) ?? string.Empty,
            user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            user.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            context.ClientIp());
    }
}
