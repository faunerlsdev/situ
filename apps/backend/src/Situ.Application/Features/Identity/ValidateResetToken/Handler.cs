using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ValidateResetToken;

public sealed class ValidateResetTokenHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ValidateResetTokenRequest, Result>
{
    public async Task<Result> Handle(ValidateResetTokenRequest request, CancellationToken cancellationToken)
    {
        var token = await db.SecurityTokens.FirstOrDefaultAsync(
            item => item.TokenHash == SecurityTokenCodec.Hash(request.Token.Trim())
                && item.Purpose == SecurityTokenPurpose.PasswordReset,
            cancellationToken);
        if (token is null)
        {
            return Result.Failure(IdentityErrors.ResetTokenNotFound());
        }

        var now = clock.UtcNow;
        if (token.ConsumedAt is not null)
        {
            return Result.Failure(IdentityErrors.TokenAlreadyUsed());
        }

        if (token.ExpiresAt <= now)
        {
            return Result.Failure(IdentityErrors.TokenExpired());
        }

        return Result.Success();
    }
}
