using Gamer.Api.Infrastructure;
using Gamer.Contracts.Auth;

namespace Gamer.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", (LoginRequest request, ITokenService tokens) =>
        {
            LoginResponse? response = tokens.TryLogin(request);
            return response is null
                ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.")
                : Results.Ok(response);
        }).AddEndpointFilter<ValidationFilter<LoginRequest>>();

        return app;
    }
}
