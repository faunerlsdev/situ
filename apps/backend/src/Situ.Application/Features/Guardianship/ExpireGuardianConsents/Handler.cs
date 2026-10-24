using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.RevokeGuardianConsent;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.ExpireGuardianConsents;

public sealed class ExpireGuardianConsentsHandler(IApplicationDbContext db, IClock clock, IEmailSender email)
    : IHandler<ExpireGuardianConsentsRequest, Result<ExpireGuardianConsentsResponse>>
{
    public async Task<Result<ExpireGuardianConsentsResponse>> Handle(
        ExpireGuardianConsentsRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = await db.GuardianConsents
            .Where(item =>
                (item.Status == GuardianConsentStatus.Requested || item.Status == GuardianConsentStatus.EmailVerified)
                && item.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var consent in due)
        {
            if (!consent.ExpireIfDue(now))
            {
                continue;
            }

            expired++;
            var user = await db.Users.FirstOrDefaultAsync(item => item.Id == consent.MinorUserId, cancellationToken);
            var guardianNotice = GuardianConsentMail.GuardianExpired();
            await RevokeGuardianConsentHandler.TrySendAsync(
                email, consent.GuardianEmail, guardianNotice.Subject, guardianNotice.Body, cancellationToken);

            var deleted = false;
            if (user is not null && user.Status == UserStatus.PendingGuardianConsent)
            {
                var minorNotice = GuardianConsentMail.MinorExpired();
                await RevokeGuardianConsentHandler.TrySendAsync(
                    email, user.Email.Value, minorNotice.Subject, minorNotice.Body, cancellationToken);

                var authorized = await db.GuardianConsents.AnyAsync(
                    item => item.MinorUserId == user.Id && item.Status == GuardianConsentStatus.Authorized,
                    cancellationToken);
                var held = await db.LegalHolds.AnyAsync(
                    item => item.TargetType == ContentTargetType.User && item.TargetId == user.Id && item.ReleasedAt == null,
                    cancellationToken);
                if (!authorized && !held)
                {
                    user.ErasePendingRegistration(now);
                    consent.RememberMinorStatement(null);
                    deleted = true;
                }
            }

            if (!await db.GuardianConsentExpiryAudits.AnyAsync(item => item.ConsentId == consent.Id, cancellationToken))
            {
                db.GuardianConsentExpiryAudits.Add(GuardianConsentExpiryAudit.Record(consent.Id, deleted, now));
            }
        }

        if (expired > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<ExpireGuardianConsentsResponse>.Success(new ExpireGuardianConsentsResponse(expired));
    }
}
