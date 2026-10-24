using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;

namespace Situ.Application.Features.Guardianship.OptOutGuardianEmail;

public sealed record OptOutGuardianEmailRequest(string Token);

public sealed class OptOutGuardianEmailHandler(IApplicationDbContext db, IClock clock)
    : IHandler<OptOutGuardianEmailRequest, Result>
{
    public async Task<Result> Handle(OptOutGuardianEmailRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var consent = await GuardianConsentLookup.ByReviewTokenAsync(db, request.Token, cancellationToken);
        if (consent is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        var email = consent.GuardianEmail;
        if (!await db.GuardianEmailOptOuts.AnyAsync(item => item.Email == email, cancellationToken))
        {
            db.GuardianEmailOptOuts.Add(GuardianEmailOptOut.Create(email, now));
        }

        if (consent.IsOpen(now))
        {
            try
            {
                consent.Decline(now, null);
                consent.RetireLink(SecurityTokenCodec.Hash(SecurityTokenCodec.CreatePlain()));
            }
            catch (DomainException exception)
            {
                return Result.Failure(GuardianErrors.FromDomain(exception));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
