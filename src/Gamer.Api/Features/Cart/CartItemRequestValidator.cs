using FluentValidation;
using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Cart;

public class CartItemRequestValidator : AbstractValidator<CartItemRequest>
{
    private const int MaxGames = 200;

    public CartItemRequestValidator()
    {
        RuleFor(r => r.Kind).Must(kind => kind is CartKinds.Game or CartKinds.Bundle).WithMessage("Tipo inválido.");
        RuleFor(r => r.Store).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.Url).NotEmpty().MaximumLength(1000)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("O endereço tem de ser http ou https.");
        RuleFor(r => r.ImageUrl).MaximumLength(1000);
        RuleFor(r => r.Price).GreaterThanOrEqualTo(0);
        RuleFor(r => r.OriginalPrice).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Currency).NotEmpty().MaximumLength(10);
        RuleFor(r => r.Games).Must(games => games is null || games.Count <= MaxGames).WithMessage("Demasiados jogos.");
        RuleForEach(r => r.Games).ChildRules(game => game.RuleFor(g => g.Title).NotEmpty().MaximumLength(300));
    }
}
