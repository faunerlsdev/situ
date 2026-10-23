using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ResetPassword;

public sealed class ResetPasswordHandler(
    IApplicationDbContext db,
    IClock clock,
    IPasswordHasher passwords,
    IEmailSender email)
    : IHandler<ResetPasswordRequest, Result>
{
    public async Task<Result> Handle(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(request.Password))
        {
            return Result.Failure(IdentityErrors.InvalidPassword());
        }

        var now = clock.UtcNow;
        var token = await db.SecurityTokens.FirstOrDefaultAsync(
            item => item.TokenHash == SecurityTokenCodec.Hash(request.Token.Trim())
                && item.Purpose == SecurityTokenPurpose.PasswordReset,
            cancellationToken);
        if (token is null)
        {
            return Result.Failure(IdentityErrors.ResetTokenNotFound());
        }

        if (!token.IsValid(now))
        {
            return Result.Failure(token.ConsumedAt is not null
                ? IdentityErrors.TokenAlreadyUsed()
                : IdentityErrors.TokenExpired());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == token.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            token.Consume(now);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Failure(IdentityErrors.ResetTokenNotFound());
        }

        user.ResetPassword(passwords.Hash(request.Password), now);
        await InvalidateOtherTokensAsync(user.Id, token.Id, now, cancellationToken);
        token.Consume(now);
        await RevokeRefreshTokensAsync(user.Id, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var (subject, body) = PasswordResetMail.Changed(user.UserName.Value);
        try
        {
            await email.SendAsync(user.Email.Value, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result.Success();
        }

        return Result.Success();
    }

    private async Task InvalidateOtherTokensAsync(
        Guid userId,
        Guid usedTokenId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var outstanding = await db.SecurityTokens
            .Where(item => item.UserId == userId && item.Id != usedTokenId && item.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var item in outstanding)
        {
            item.Invalidate(now);
        }
    }

    private async Task RevokeRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var refreshTokens = await db.RefreshTokens
            .Where(item => item.UserId == userId && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.Revoke(now);
        }
    }
}
