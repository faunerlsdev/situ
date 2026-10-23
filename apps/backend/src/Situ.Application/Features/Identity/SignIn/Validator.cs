using FluentValidation;

namespace Situ.Application.Features.Identity.SignIn;

public sealed class SignInValidator : AbstractValidator<SignInRequest>
{
    public SignInValidator()
    {
        RuleFor(request => request.Login).NotEmpty().MaximumLength(254);
        RuleFor(request => request.Password).NotEmpty();
        RuleFor(request => request.DeviceIdentifier).NotEmpty().MaximumLength(128);
    }
}
