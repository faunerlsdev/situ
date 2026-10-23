using FluentValidation;

namespace Situ.Application.Features.Identity.SignOut;

public sealed class SignOutValidator : AbstractValidator<SignOutRequest>
{
    public SignOutValidator()
    {
        RuleFor(request => request.RefreshToken).NotEmpty();
    }
}
