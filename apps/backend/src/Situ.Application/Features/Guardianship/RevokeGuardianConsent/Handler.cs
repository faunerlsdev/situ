using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.RevokeGuardianConsent;

public sealed record RevokeGuardianConsentRequest(string Token);

public sealed record UpdateGuardianPermissionsRequest(
    string Token,
    bool LiveStreamingAuthorized,
    bool LocationSharingAuthorized);

public sealed class RevokeGuardianConsentHandler(IApplicationDbContext db, IClock clock, IEmailSender email)
    : IHandler<RevokeGuardianConsentRequest, Result>
{
    public async Task<Result> Handle(RevokeGuardianConsentRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var found = await FindAsync(db, request.Token, now, cancellationToken);
        if (found is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        var (token, consent, user) = found.Value;
        try
        {
            consent.Revoke(now);
            token.Consume(now);
            user.RevokeGuardianConsent(now);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        await db.SaveChangesAsync(cancellationToken);
        var (subject, body) = GuardianConsentMail.Revoked(user.UserName.Value);
        await TrySendAsync(email, consent.GuardianContactEmail ?? consent.GuardianEmail, subject, body, cancellationToken);
        if (user.Status != UserStatus.Deleted)
        {
            await TrySendAsync(email, user.Email.Value, subject, body, cancellationToken);
        }

        return Result.Success();
    }

    internal static async Task<(SecurityToken Token, GuardianConsent Consent, User User)?> FindAsync(
        IApplicationDbContext db,
        string? plain,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plain))
        {
            return null;
        }

        var hash = SecurityTokenCodec.Hash(plain.Trim());
        var token = await db.SecurityTokens.FirstOrDefaultAsync(
            item => item.TokenHash == hash && item.Purpose == SecurityTokenPurpose.GuardianConsentManagement,
            cancellationToken);
        if (token is null || !token.IsValid(now))
        {
            return null;
        }

        var consent = await db.GuardianConsents
            .Where(item => item.MinorUserId == token.UserId && item.Status == GuardianConsentStatus.Authorized)
            .OrderByDescending(item => item.AuthorizedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var user = consent is null
            ? null
            : await db.Users.FirstOrDefaultAsync(item => item.Id == consent.MinorUserId, cancellationToken);
        if (consent is null || user is null)
        {
            return null;
        }

        return (token, consent, user);
    }

    internal static async Task TrySendAsync(
        IEmailSender email,
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            await email.SendAsync(to, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The decision is already stored.
        }
    }
}

public sealed class UpdateGuardianPermissionsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpdateGuardianPermissionsRequest, Result>
{
    public async Task<Result> Handle(UpdateGuardianPermissionsRequest request, CancellationToken cancellationToken)
    {
        var found = await RevokeGuardianConsentHandler.FindAsync(db, request.Token, clock.UtcNow, cancellationToken);
        if (found is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        try
        {
            found.Value.Consent.UpdatePermissions(request.LiveStreamingAuthorized, request.LocationSharingAuthorized, clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
