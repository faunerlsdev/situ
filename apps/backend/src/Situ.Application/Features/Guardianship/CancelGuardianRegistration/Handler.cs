using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.CancelGuardianRegistration;

public sealed record CancelGuardianRegistrationRequest;

public sealed class CancelGuardianRegistrationHandler(IApplicationDbContext db, IClock clock, ICurrentUser currentUser)
    : IHandler<CancelGuardianRegistrationRequest, Result>
{
    public async Task<Result> Handle(CancelGuardianRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status != UserStatus.PendingGuardianConsent)
        {
            return Result.Failure(GuardianErrors.NotPending());
        }

        var held = await db.LegalHolds.AnyAsync(
            item => item.TargetType == ContentTargetType.User && item.TargetId == userId && item.ReleasedAt == null,
            cancellationToken);
        if (held)
        {
            return Result.Failure(GuardianErrors.LegalHold());
        }

        var consents = await db.GuardianConsents.Where(item => item.MinorUserId == userId).ToListAsync(cancellationToken);
        db.GuardianConsents.RemoveRange(consents);
        var tokens = await db.SecurityTokens.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        db.SecurityTokens.RemoveRange(tokens);
        var dispatches = await db.GuardianConsentDispatches.Where(item => item.MinorUserId == userId).ToListAsync(cancellationToken);
        db.GuardianConsentDispatches.RemoveRange(dispatches);

        try
        {
            user.ErasePendingRegistration(clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
