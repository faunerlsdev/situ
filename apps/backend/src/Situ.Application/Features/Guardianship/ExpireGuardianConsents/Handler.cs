using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Guardianship;

namespace Situ.Application.Features.Guardianship.ExpireGuardianConsents;

public sealed class ExpireGuardianConsentsHandler(IApplicationDbContext db, IClock clock)
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
            if (consent.ExpireIfDue(now))
            {
                expired++;
            }
        }

        if (expired > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<ExpireGuardianConsentsResponse>.Success(new ExpireGuardianConsentsResponse(expired));
    }
}
