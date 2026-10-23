using FluentValidation;

namespace Situ.Application.Features.Guardianship.SubmitGuardianData;

public sealed class SubmitGuardianDataValidator : AbstractValidator<SubmitGuardianDataRequest>
{
    public SubmitGuardianDataValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
        RuleFor(request => request.FullName).NotEmpty().MaximumLength(150);
        RuleFor(request => request.DocumentNumber).NotEmpty().MaximumLength(30);
        RuleFor(request => request.ContactEmail).NotEmpty().MaximumLength(254);
    }
}
