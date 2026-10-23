using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Legal;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Legal;

namespace Situ.Application.Features.Identity.RegisterUser;

public sealed class RegisterUserHandler(
    IApplicationDbContext db,
    IClock clock,
    IPasswordHasher passwords,
    IEmailSender email,
    IPublicLinks links)
    : IHandler<RegisterUserRequest, Result<RegisterUserResponse>>
{
    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(request.Password))
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.InvalidPassword());
        }

        if (!Email.TryCreate(request.Email, out var emailAddress))
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.InvalidEmail());
        }

        if (!UserName.TryCreate(request.UserName, out var userName))
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.InvalidUserName());
        }

        var country = await db.Countries
            .FirstOrDefaultAsync(item => item.Id == request.CountryId && item.IsEnabled, cancellationToken);
        if (country is null)
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.CountryUnavailable());
        }

        var region = await db.Regions
            .FirstOrDefaultAsync(item => item.Id == request.RegionId, cancellationToken);
        if (region is null || !region.IsEnabled || region.CountryId != country.Id)
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.RegionUnavailable());
        }

        var city = await db.Cities
            .FirstOrDefaultAsync(item => item.Id == request.CityId, cancellationToken);
        if (city is null || !city.IsEnabled || city.RegionId != region.Id || city.CountryId != country.Id)
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.CityUnavailable());
        }

        if (await db.Users.AnyAsync(user => user.Email == emailAddress, cancellationToken))
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.EmailTaken());
        }

        if (await db.Users.AnyAsync(user => user.UserName == userName, cancellationToken))
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.UserNameTaken());
        }

        var published = await db.LegalDocuments.Where(document => document.IsActive).ToListAsync(cancellationToken);
        var current = ApplicableLegalDocuments.SelectCurrent(published, country.Id);
        if (current.Count == 0)
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.DocumentsUnavailable());
        }

        var accepted = request.AcceptedDocumentIds.ToHashSet();
        if (current.Count != accepted.Count || current.Any(document => !accepted.Contains(document.Id)))
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.DocumentsNotAccepted());
        }

        var now = clock.UtcNow;
        User user;
        try
        {
            user = User.Register(
                emailAddress,
                userName,
                passwords.Hash(request.Password),
                request.DateOfBirth,
                country.Id,
                region.Id,
                city.Id,
                country.DefaultLocale,
                country.MinimumAge,
                now);
        }
        catch (DomainException exception)
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.FromDomain(exception));
        }

        db.Users.Add(user);

        foreach (var document in current)
        {
            db.ConsentRecords.Add(ConsentRecord.Accept(
                user.Id,
                document.Type,
                document.Version,
                document.CountryId,
                ConsentMethod.Registration,
                Clip(request.IpAddress, 64),
                Clip(request.UserAgent, 512),
                now));
        }

        var plainToken = SecurityTokenCodec.CreatePlain();
        db.SecurityTokens.Add(SecurityToken.Issue(
            user.Id,
            SecurityTokenPurpose.EmailConfirmation,
            SecurityTokenCodec.Hash(plainToken),
            now,
            EmailConfirmationPolicy.TokenLifetime));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<RegisterUserResponse>.Failure(
                Error.Conflict("user.already_exists", "Ya existe una cuenta con ese correo o usuario."));
        }

        if (!await TrySendAsync(emailAddress.Value, userName.Value, plainToken, cancellationToken))
        {
            return Result<RegisterUserResponse>.Failure(IdentityErrors.EmailSendFailed());
        }

        return Result<RegisterUserResponse>.Success(new RegisterUserResponse(user.Id, user.Status));
    }

    private async Task<bool> TrySendAsync(
        string to,
        string userName,
        string plainToken,
        CancellationToken cancellationToken)
    {
        var (subject, body) = EmailConfirmationMail.Compose(userName, links.EmailConfirmation(plainToken));
        try
        {
            await email.SendAsync(to, subject, body, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
