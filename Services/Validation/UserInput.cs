namespace StayBook.Services.Validation;

public sealed record UserInput(string Name, string Email, string Role, bool Active, string? Password);
