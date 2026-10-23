using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;

namespace Situ.Application.Features.Guardianship.DeclineGuardianConsent;

public sealed class DeclineGuardianConsentHandler(IApplicationDbContext db, IClock clock)
    : IHandler<DeclineGuardianConsentRequest, Result>
{
    public async Task<Result> Handle(DeclineGuardianConsentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        var hash = SecurityTokenCodec.Hash(request.Token.Trim());
        var consent = await db.GuardianConsents.FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (consent is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        try
        {
            consent.Decline(clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
