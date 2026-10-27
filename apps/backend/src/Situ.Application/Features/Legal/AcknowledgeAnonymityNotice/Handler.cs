using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity;
using Situ.Application.Common.Results;
using Situ.Domain.Identity;
using Situ.Domain.Legal;

namespace Situ.Application.Features.Legal.AcknowledgeAnonymityNotice;

public static class AnonymityNotice
{
    public const string Version = "2026-10-27";
}

public sealed record AcknowledgeAnonymityNoticeRequest(Guid UserId, string? IpAddress, string? UserAgent);

public sealed record AcknowledgeAnonymityNoticeResponse(string Version, DateTimeOffset AcceptedAt);

public sealed class AcknowledgeAnonymityNoticeHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AcknowledgeAnonymityNoticeRequest, Result<AcknowledgeAnonymityNoticeResponse>>
{
    public async Task<Result<AcknowledgeAnonymityNoticeResponse>> Handle(
        AcknowledgeAnonymityNoticeRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return Result<AcknowledgeAnonymityNoticeResponse>.Failure(AuthErrors.Unauthenticated());
        }

        var existing = await db.ConsentRecords
            .Where(item => item.UserId == user.Id && item.DocumentType == LegalDocumentType.AnonymityNotice && item.RevokedAt == null)
            .OrderByDescending(item => item.AcceptedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return Result<AcknowledgeAnonymityNoticeResponse>.Success(new AcknowledgeAnonymityNoticeResponse(existing.Version, existing.AcceptedAt));
        }

        var now = clock.UtcNow;
        var record = ConsentRecord.Accept(
            user.Id,
            LegalDocumentType.AnonymityNotice,
            AnonymityNotice.Version,
            user.CountryId,
            ConsentMethod.FirstUse,
            request.IpAddress,
            request.UserAgent,
            now);
        db.ConsentRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return Result<AcknowledgeAnonymityNoticeResponse>.Success(new AcknowledgeAnonymityNoticeResponse(record.Version, record.AcceptedAt));
    }
}
