using System.ComponentModel.DataAnnotations;

namespace Gamer.Api.Features.Auth;

public class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required, MinLength(32)]
    public string JwtKey { get; init; } = string.Empty;

    public string Issuer { get; init; } = "Gamer";

    public int ExpiryDays { get; init; } = 30;
}
