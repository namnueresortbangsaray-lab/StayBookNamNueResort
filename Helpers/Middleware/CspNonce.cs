using System.Security.Cryptography;

namespace StayBook.Helpers.Middleware;

/// <summary>Per-request nonce for <c>script-src</c>; render it on every <c>&lt;script&gt;</c> tag.</summary>
public sealed class CspNonce
{
    public string Value { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
}
