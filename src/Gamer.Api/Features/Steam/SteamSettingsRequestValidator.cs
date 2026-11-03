using FluentValidation;
using Gamer.Contracts.Steam;

namespace Gamer.Api.Features.Steam;

public class SteamSettingsRequestValidator : AbstractValidator<SteamSettingsRequest>
{
    public SteamSettingsRequestValidator()
    {
        RuleFor(r => r.SteamId).Matches(@"^\d{17}$").WithMessage("O SteamID64 tem 17 dígitos.");
        RuleFor(r => r.RefreshHours).InclusiveBetween(0, 168);
        RuleFor(r => r.ApiKey).MaximumLength(100);
    }
}
