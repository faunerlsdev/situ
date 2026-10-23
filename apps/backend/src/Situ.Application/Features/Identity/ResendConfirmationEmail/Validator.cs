using FluentValidation;

namespace Situ.Application.Features.Identity.ResendConfirmationEmail;

public sealed class ResendConfirmationEmailValidator : AbstractValidator<ResendConfirmationEmailRequest>
{
    public ResendConfirmationEmailValidator()
    {
        RuleFor(request => request.Email).NotEmpty();
    }
}
