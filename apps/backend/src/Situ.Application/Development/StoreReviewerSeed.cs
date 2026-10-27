using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;

namespace Situ.Application.Development;

/// <summary>
/// Adult review account for App Store Connect and Play Console. Runs only when the operator
/// invokes store:seed-reviewer. The password is returned once and is not written to a log.
/// </summary>
public static class StoreReviewerSeed
{
    public const string EmailAddress = "revisor.tiendas@situ.test";
    public const string UserNameValue = "revisor_tiendas";

    public static async Task<Result<string>> ApplyAsync(
        IApplicationDbContext db,
        IPasswordHasher passwords,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var city = await db.Cities.FirstOrDefaultAsync(item => item.Name == "Bogotá", cancellationToken);
        if (city is null)
        {
            return Result<string>.Failure(Error.NotFound(
                "store_reviewer.city",
                "No está la ciudad Bogotá. Corre las migraciones antes de store:seed-reviewer."));
        }

        var country = await db.Countries.FirstAsync(item => item.Id == city.CountryId, cancellationToken);
        var now = clock.UtcNow;
        var password = CreatePassword();
        var email = Email.Create(EmailAddress);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (user is { Status: UserStatus.Deleted })
        {
            return Result<string>.Failure(Error.Conflict(
                "store_reviewer.deleted",
                "La cuenta de revisión ya fue eliminada y no se puede rehacer."));
        }

        if (user is null)
        {
            var handle = UserName.Create(UserNameValue);
            var taken = await db.Users.AnyAsync(item => item.UserName == handle, cancellationToken);
            if (taken)
            {
                return Result<string>.Failure(Error.Conflict(
                    "store_reviewer.username_taken",
                    "El usuario revisor_tiendas ya pertenece a otra cuenta."));
            }

            user = User.Register(
                email,
                handle,
                PersonName.Create("Revisor"),
                PersonName.Create("Tiendas"),
                PhoneNumber.Create("+573001110014"),
                passwords.Hash(password),
                new DateOnly(1990, 1, 15),
                country.Id,
                city.RegionId,
                city.Id,
                "es-CO",
                country.MinimumAge,
                now);
            user.ConfirmEmail(now);
            db.Users.Add(user);
            db.UserProfiles.Add(UserProfile.CreateDefault(user.Id, "Revisor Tiendas"));
        }
        else
        {
            if (user.Status is UserStatus.Suspended or UserStatus.Banned)
            {
                user.Reinstate(now);
            }

            if (user.Status == UserStatus.DeletionRequested)
            {
                user.CancelDeletion(now);
            }

            user.ConfirmEmail(now);
            user.ResetPassword(passwords.Hash(password), now);
            user.ResetTwoFactor();
            var codes = await db.TwoFactorRecoveryCodes.Where(item => item.UserId == user.Id).ToListAsync(cancellationToken);
            db.TwoFactorRecoveryCodes.RemoveRange(codes);
            if (!await db.UserProfiles.AnyAsync(item => item.UserId == user.Id, cancellationToken))
            {
                db.UserProfiles.Add(UserProfile.CreateDefault(user.Id, "Revisor Tiendas"));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(password);
    }

    private static string CreatePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(24);
        var chars = new char[24];
        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = alphabet[bytes[index] % alphabet.Length];
        }

        var password = "Aa1" + new string(chars);
        return PasswordPolicy.IsValid(password) ? password : CreatePassword();
    }
}
