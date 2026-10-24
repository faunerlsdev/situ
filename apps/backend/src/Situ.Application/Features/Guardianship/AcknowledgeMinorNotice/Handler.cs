using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.GetMyGuardianConsentStatus;
using Situ.Application.Features.Guardianship.RequestGuardianConsent;
using Situ.Application.Features.Identity;
using Situ.Domain.Identity;
using Situ.Domain.Legal;

namespace Situ.Application.Features.Guardianship.AcknowledgeMinorNotice;

public sealed record AcknowledgeMinorNoticeRequest(string? IpAddress, string? UserAgent);

public sealed class AcknowledgeMinorNoticeHandler(IApplicationDbContext db, IClock clock, ICurrentUser currentUser)
    : IHandler<AcknowledgeMinorNoticeRequest, Result<GuardianConsentStatusResponse>>
{
    public async Task<Result<GuardianConsentStatusResponse>> Handle(
        AcknowledgeMinorNoticeRequest request,
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

        var exists = await db.ConsentRecords.AnyAsync(
            item => item.UserId == userId && item.DocumentType == LegalDocumentType.GuardianNotice && item.RevokedAt == null,
            cancellationToken);
        if (!exists)
        {
            db.ConsentRecords.Add(ConsentRecord.Accept(
                userId,
                LegalDocumentType.GuardianNotice,
                GuardianConsentRequests.NoticeVersion,
                user.CountryId,
                ConsentMethod.GuardianFlow,
                request.IpAddress,
                request.UserAgent,
                clock.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }

        return await new GetMyGuardianConsentStatusHandler(db, clock, currentUser)
            .Handle(new GetMyGuardianConsentStatusRequest(), cancellationToken);
    }
}
