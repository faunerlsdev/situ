using FluentValidation;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty();
        RuleFor(request => request.NewPassword)
            .Must(PasswordPolicy.IsValid)
            .WithMessage("La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula y un número.");
    }
}
