using FluentValidation;

namespace Situ.Application.Features.Guardianship.DeclineGuardianConsent;

public sealed class DeclineGuardianConsentValidator : AbstractValidator<DeclineGuardianConsentRequest>
{
    public DeclineGuardianConsentValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
    }
}
