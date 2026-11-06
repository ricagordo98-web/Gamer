using FluentValidation;
using Gamer.Api.Data;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Games;

public class BulkAddRequestValidator : AbstractValidator<BulkAddRequest>
{
    public const int MaxTitles = 1000;

    public BulkAddRequestValidator(GamerDbContext db)
    {
        RuleFor(r => r.Titles).NotNull().Must(t => t is { Count: > 0 and <= MaxTitles })
            .WithMessage($"Indica entre 1 e {MaxTitles} títulos.");
        RuleFor(r => r.Status).IsInEnum();
        RuleFor(r => r.PlatformId)
            .MustAsync((platformId, cancellationToken) => db.Platforms.AnyAsync(p => p.Id == platformId, cancellationToken))
            .WithMessage("Platform does not exist.");
    }
}
