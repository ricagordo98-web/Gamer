using FluentValidation;
using Gamer.Api.Data;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Games;

public class GameRequestValidator : AbstractValidator<GameRequest>
{
    public const int MaxTitleLength = 200;
    public const int MaxNotesLength = 2000;
    public const int MinRating = 1;
    public const int MaxRating = 10;

    public GameRequestValidator(GamerDbContext db)
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(MaxTitleLength);
        RuleFor(r => r.Notes).MaximumLength(MaxNotesLength);
        RuleFor(r => r.Status).IsInEnum();
        RuleFor(r => r.Rating).InclusiveBetween(MinRating, MaxRating).When(r => r.Rating is not null);
        RuleFor(r => r.PlatformId)
            .MustAsync((platformId, cancellationToken) => db.Platforms.AnyAsync(p => p.Id == platformId, cancellationToken))
            .WithMessage("Platform does not exist.");
    }
}
