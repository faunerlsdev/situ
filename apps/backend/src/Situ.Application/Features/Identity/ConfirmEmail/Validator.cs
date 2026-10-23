using FluentValidation;

namespace Situ.Application.Features.Identity.ConfirmEmail;

public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
    }
}
