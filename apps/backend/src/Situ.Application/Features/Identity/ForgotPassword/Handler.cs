using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ForgotPassword;

public sealed class ForgotPasswordHandler(
    IApplicationDbContext db,
    IClock clock,
    IEmailSender email,
    IPublicLinks links)
    : IHandler<ForgotPasswordRequest, Result>
{
    public async Task<Result> Handle(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!Email.TryCreate(request.Email, out var emailAddress))
        {
            return Result.Success();
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == emailAddress, cancellationToken);
        if (user is null || user.Status is UserStatus.Deleted or UserStatus.Banned or UserStatus.DeletionRequested)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        var latestExpiry = await db.SecurityTokens
            .Where(item =>
                item.UserId == user.Id
                && item.Purpose == SecurityTokenPurpose.PasswordReset
                && item.ConsumedAt == null)
            .OrderByDescending(item => item.ExpiresAt)
            .Select(item => (DateTimeOffset?)item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestExpiry is { } expires
            && now - (expires - PasswordResetPolicy.TokenLifetime) < PasswordResetPolicy.MinimumRequestInterval)
        {
            return Result.Success();
        }

        var previous = await db.SecurityTokens
            .Where(item =>
                item.UserId == user.Id
                && item.Purpose == SecurityTokenPurpose.PasswordReset
                && item.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in previous)
        {
            token.Invalidate(now);
        }

        var plainToken = SecurityTokenCodec.CreatePlain();
        var issued = SecurityToken.Issue(
            user.Id,
            SecurityTokenPurpose.PasswordReset,
            SecurityTokenCodec.Hash(plainToken),
            now,
            PasswordResetPolicy.TokenLifetime);
        db.SecurityTokens.Add(issued);
        await db.SaveChangesAsync(cancellationToken);

        var (subject, body) = PasswordResetMail.Link(user.UserName.Value, links.PasswordReset(plainToken));
        try
        {
            await email.SendAsync(emailAddress.Value, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            issued.Invalidate(now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
