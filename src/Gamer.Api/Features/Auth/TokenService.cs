using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Gamer.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gamer.Api.Features.Auth;

public class TokenService(IOptions<AuthOptions> options, TimeProvider clock) : ITokenService
{
    private readonly AuthOptions _options = options.Value;

    public LoginResponse? TryLogin(LoginRequest request)
    {
        bool valid = FixedTimeEquals(request.Username, _options.Username)
            & FixedTimeEquals(request.Password, _options.Password);

        return valid ? CreateToken(request.Username) : null;
    }

    public static SymmetricSecurityKey CreateKey(string jwtKey) => new(Encoding.UTF8.GetBytes(jwtKey));

    private LoginResponse CreateToken(string username)
    {
        DateTimeOffset expiresAt = clock.GetUtcNow().AddDays(_options.ExpiryDays);
        SigningCredentials credentials = new(CreateKey(_options.JwtKey), SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Issuer,
            claims: [new Claim(ClaimTypes.Name, username)],
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        byte[] rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}

