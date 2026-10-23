using FluentValidation;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.RegisterUser;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(request => request.Email).NotEmpty();
        RuleFor(request => request.UserName).NotEmpty();
        RuleFor(request => request.Password)
            .Must(PasswordPolicy.IsValid)
            .WithMessage("La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula y un número.");
        RuleFor(request => request.DateOfBirth).NotEqual(default(DateOnly));
        RuleFor(request => request.CountryId).NotEmpty();
        RuleFor(request => request.RegionId).NotEmpty();
        RuleFor(request => request.CityId).NotEmpty();
        RuleFor(request => request.AcceptedDocumentIds).NotEmpty();
    }
}
