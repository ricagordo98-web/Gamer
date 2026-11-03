using Gamer.Contracts.Auth;

namespace Gamer.Api.Features.Auth;

public interface ITokenService
{
    LoginResponse? TryLogin(LoginRequest request);
}
