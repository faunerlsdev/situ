using FluentValidation;

namespace Situ.Application.Features.Guardianship.AuthorizeGuardianConsent;

public sealed class AuthorizeGuardianConsentValidator : AbstractValidator<AuthorizeGuardianConsentRequest>
{
    public AuthorizeGuardianConsentValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
    }
}
