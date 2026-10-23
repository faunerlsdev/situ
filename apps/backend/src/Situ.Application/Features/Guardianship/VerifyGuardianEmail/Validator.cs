using FluentValidation;

namespace Situ.Application.Features.Guardianship.VerifyGuardianEmail;

public sealed class VerifyGuardianEmailValidator : AbstractValidator<VerifyGuardianEmailRequest>
{
    public VerifyGuardianEmailValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
    }
}
