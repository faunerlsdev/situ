using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Guardianship;
using Situ.Domain.Legal;

namespace Situ.Application.Features.Guardianship.GetMyGuardianConsentStatus;

public sealed record GetMyGuardianConsentStatusRequest;

public sealed record GuardianConsentStatusResponse(
    string Status,
    string? MaskedEmail,
    DateTimeOffset? ExpiresAt,
    int ResendsRemaining,
    DateTimeOffset? RetryAfter,
    bool MinorNoticeRecorded);

public sealed class GetMyGuardianConsentStatusHandler(IApplicationDbContext db, IClock clock, ICurrentUser currentUser)
    : IHandler<GetMyGuardianConsentStatusRequest, Result<GuardianConsentStatusResponse>>
{
    public async Task<Result<GuardianConsentStatusResponse>> Handle(
        GetMyGuardianConsentStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<GuardianConsentStatusResponse>.Failure(AuthErrors.Unauthenticated());
        }

        var now = clock.UtcNow;
        var consent = await db.GuardianConsents
            .Where(item => item.MinorUserId == userId)
            .OrderByDescending(item => item.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (consent is not null && consent.ExpireIfDue(now))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var rules = await GuardianLimitRules.LoadAsync(db, cancellationToken);
        var since = now.AddHours(-24);
        var sent = await db.GuardianConsentDispatches.CountAsync(
            item => item.MinorUserId == userId && item.SentAt >= since,
            cancellationToken);
        var heard = await db.ConsentRecords.AnyAsync(
            item => item.UserId == userId && item.DocumentType == LegalDocumentType.GuardianNotice && item.RevokedAt == null,
            cancellationToken);
        var retryAfter = await GuardianDispatchGate.NextRetryAsync(db, userId, now, cancellationToken);

        if (consent is null)
        {
            return Result<GuardianConsentStatusResponse>.Success(new GuardianConsentStatusResponse(
                "NotRequested",
                null,
                null,
                Math.Max(0, rules.MaxSendsPerMinorPerDay - sent),
                retryAfter,
                heard));
        }

        return Result<GuardianConsentStatusResponse>.Success(new GuardianConsentStatusResponse(
            consent.Status.ToString(),
            Mask(consent.GuardianEmail),
            consent.ExpiresAt,
            Math.Max(0, rules.MaxSendsPerMinorPerDay - sent),
            retryAfter,
            heard));
    }

    internal static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        return email[0] + "***" + email[at..];
    }
}
