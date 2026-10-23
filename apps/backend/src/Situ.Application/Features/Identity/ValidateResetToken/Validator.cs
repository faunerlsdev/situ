using FluentValidation;

namespace Situ.Application.Features.Identity.ValidateResetToken;

public sealed class ValidateResetTokenValidator : AbstractValidator<ValidateResetTokenRequest>
{
    public ValidateResetTokenValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
    }
}
