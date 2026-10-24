using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.VerifyGuardianEmailCode;

public sealed record VerifyGuardianEmailCodeRequest(string Token, string Code);

public sealed class VerifyGuardianEmailCodeHandler(IApplicationDbContext db, IClock clock)
    : IHandler<VerifyGuardianEmailCodeRequest, Result>
{
    public async Task<Result> Handle(VerifyGuardianEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var consent = await GuardianConsentLookup.ByReviewTokenAsync(db, request.Token, cancellationToken);
        if (consent is null || !consent.IsOpen(now))
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        var rules = await GuardianLimitRules.LoadAsync(db, cancellationToken);
        var code = (request.Code ?? "").Trim();
        var match = await db.SecurityTokens.FirstOrDefaultAsync(
            item => item.UserId == consent.MinorUserId
                && item.Purpose == SecurityTokenPurpose.GuardianEmailCode
                && item.TokenHash == SecurityTokenCodec.Hash(code)
                && item.ConsumedAt == null
                && item.ExpiresAt > now,
            cancellationToken);
        if (match is null)
        {
            var current = await db.SecurityTokens
                .Where(item => item.UserId == consent.MinorUserId
                    && item.Purpose == SecurityTokenPurpose.GuardianEmailCode
                    && item.ConsumedAt == null
                    && item.ExpiresAt > now)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (current is not null)
            {
                try
                {
                    current.RegisterFailedAttempt(now, rules.MaxCodeAttempts);
                }
                catch (DomainException)
                {
                    return Result.Failure(GuardianErrors.CodeRejected());
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure(GuardianErrors.CodeRejected());
        }

        try
        {
            match.Consume(now);
            consent.VerifyGuardianEmail(now);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
