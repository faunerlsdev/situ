using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.GetMyGuardianConsentStatus;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Legal;

namespace Situ.Application.Features.Guardianship.RequestGuardianConsent;

public sealed record RequestGuardianConsentRequest(
    string GuardianEmail,
    bool MinorNoticeAccepted,
    string? MinorStatement = null,
    string? IpAddress = null);

public sealed class RequestGuardianConsentHandler(
    IApplicationDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IEmailSender email,
    IPublicLinks links)
    : IHandler<RequestGuardianConsentRequest, Result<GuardianConsentStatusResponse>>
{
    public async Task<Result<GuardianConsentStatusResponse>> Handle(
        RequestGuardianConsentRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<GuardianConsentStatusResponse>.Failure(AuthErrors.Unauthenticated());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status != UserStatus.PendingGuardianConsent)
        {
            return Result<GuardianConsentStatusResponse>.Failure(GuardianErrors.NotPending());
        }

        if (!request.MinorNoticeAccepted)
        {
            return Result<GuardianConsentStatusResponse>.Failure(GuardianErrors.NoticeRequired());
        }

        var now = clock.UtcNow;
        var created = await GuardianConsentRequests.OpenAsync(
            db,
            email,
            links,
            user,
            request.GuardianEmail,
            now,
            request.MinorStatement,
            request.IpAddress,
            cancellationToken);
        if (created.IsFailure)
        {
            return Result<GuardianConsentStatusResponse>.Failure(created.Error!);
        }

        return await new GetMyGuardianConsentStatusHandler(db, clock, currentUser).Handle(
            new GetMyGuardianConsentStatusRequest(),
            cancellationToken);
    }
}

internal static class GuardianConsentRequests
{
    public const string NoticeVersion = "2026-10";

    public static async Task<Result<string>> OpenAsync(
        IApplicationDbContext db,
        IEmailSender email,
        IPublicLinks links,
        User minor,
        string guardianEmail,
        DateTimeOffset now,
        string? minorStatement,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (!Email.TryCreate(guardianEmail, out var parsed) || parsed.Value.Equals(minor.Email.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure(GuardianErrors.GuardianEmailRejected());
        }

        var blocked = await GuardianDispatchGate.RejectionAsync(
            db,
            minor.Id,
            parsed.Value,
            ipAddress,
            emailChange: false,
            now,
            cancellationToken);
        if (blocked is not null)
        {
            return Result<string>.Failure(blocked);
        }

        var existing = await db.GuardianConsents
            .Where(item => item.MinorUserId == minor.Id)
            .OrderByDescending(item => item.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null && existing.IsOpen(now))
        {
            return Result<string>.Failure(GuardianErrors.AlreadyOpen());
        }

        if (existing?.Status == GuardianConsentStatus.Declined)
        {
            return Result<string>.Failure(GuardianErrors.Declined());
        }

        var plain = SecurityTokenCodec.CreatePlain();
        var consent = GuardianConsent.Request(
            minor.Id,
            parsed.Value,
            SecurityTokenCodec.Hash(plain),
            NoticeVersion,
            now,
            now);
        consent.RememberMinorStatement(minorStatement);
        db.GuardianConsents.Add(consent);
        db.GuardianConsentDispatches.Add(GuardianConsentDispatch.Record(
            minor.Id,
            parsed.Value,
            ipAddress,
            GuardianDispatchKind.Request,
            now));
        if (!await db.ConsentRecords.AnyAsync(
                item => item.UserId == minor.Id && item.DocumentType == LegalDocumentType.GuardianNotice && item.RevokedAt == null,
                cancellationToken))
        {
            db.ConsentRecords.Add(ConsentRecord.Accept(
                minor.Id,
                LegalDocumentType.GuardianNotice,
                NoticeVersion,
                minor.CountryId,
                ConsentMethod.GuardianFlow,
                ipAddress,
                null,
                now));
        }

        await db.SaveChangesAsync(cancellationToken);

        var (subject, body) = GuardianConsentMail.Request(links.GuardianReview(plain));
        try
        {
            await email.SendAsync(parsed.Value, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<string>.Failure(GuardianErrors.EmailSendFailed());
        }

        return Result<string>.Success(parsed.Value);
    }
}
