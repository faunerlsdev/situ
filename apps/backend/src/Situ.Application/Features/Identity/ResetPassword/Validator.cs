using FluentValidation;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ResetPassword;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(request => request.Token).NotEmpty();
        RuleFor(request => request.Password)
            .Must(PasswordPolicy.IsValid)
            .WithMessage("La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula y un número.");
    }
}
