namespace Gamer.Contracts.Auth;

public record LoginResponse(string Token, DateTimeOffset ExpiresAt);
