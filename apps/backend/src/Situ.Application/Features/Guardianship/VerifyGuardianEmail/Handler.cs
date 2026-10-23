using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.VerifyGuardianEmail;

public sealed class VerifyGuardianEmailHandler(IApplicationDbContext db, IClock clock)
    : IHandler<VerifyGuardianEmailRequest, Result>
{
    public async Task<Result> Handle(VerifyGuardianEmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure(GuardianErrors.VerificationNotFound());
        }

        var now = clock.UtcNow;
        var token = await db.SecurityTokens.FirstOrDefaultAsync(
            item => item.TokenHash == SecurityTokenCodec.Hash(request.Token.Trim())
                && item.Purpose == SecurityTokenPurpose.GuardianEmailVerification,
            cancellationToken);
        if (token is null)
        {
            return Result.Failure(GuardianErrors.VerificationNotFound());
        }

        if (token.ConsumedAt is not null)
        {
            return Result.Failure(IdentityErrors.TokenAlreadyUsed());
        }

        if (!token.IsValid(now))
        {
            return Result.Failure(IdentityErrors.TokenExpired());
        }

        var consent = await db.GuardianConsents
            .Where(item =>
                item.MinorUserId == token.UserId
                && (item.Status == GuardianConsentStatus.Requested || item.Status == GuardianConsentStatus.EmailVerified))
            .OrderByDescending(item => item.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (consent is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        try
        {
            consent.VerifyGuardianEmail(now);
            token.Consume(now);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
