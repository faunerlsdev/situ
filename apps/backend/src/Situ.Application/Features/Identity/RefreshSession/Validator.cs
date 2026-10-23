using FluentValidation;

namespace Situ.Application.Features.Identity.RefreshSession;

public sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionRequest>
{
    public RefreshSessionValidator()
    {
        RuleFor(request => request.RefreshToken).NotEmpty();
    }
}
