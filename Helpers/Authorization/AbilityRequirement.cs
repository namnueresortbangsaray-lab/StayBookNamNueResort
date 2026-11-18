using Microsoft.AspNetCore.Authorization;

namespace StayBook.Helpers.Authorization;

public sealed record AbilityRequirement(string Ability) : IAuthorizationRequirement;
