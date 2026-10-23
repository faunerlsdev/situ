using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ResendConfirmationEmail;

public sealed class ResendConfirmationEmailHandler(
    IApplicationDbContext db,
    IClock clock,
    IEmailSender email,
    IPublicLinks links)
    : IHandler<ResendConfirmationEmailRequest, Result>
{
    public async Task<Result> Handle(ResendConfirmationEmailRequest request, CancellationToken cancellationToken)
    {
        if (!Email.TryCreate(request.Email, out var emailAddress))
        {
            return Result.Success();
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == emailAddress, cancellationToken);
        if (user is null || user.EmailConfirmedAt is not null || user.Status is UserStatus.Deleted or UserStatus.Banned)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        var latestExpiry = await db.SecurityTokens
            .Where(item => item.UserId == user.Id && item.Purpose == SecurityTokenPurpose.EmailConfirmation)
            .OrderByDescending(item => item.ExpiresAt)
            .Select(item => (DateTimeOffset?)item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestExpiry is { } expires
            && now - (expires - EmailConfirmationPolicy.TokenLifetime) < EmailConfirmationPolicy.MinimumResendInterval)
        {
            return Result.Failure(IdentityErrors.ConfirmationTooSoon());
        }

        var previous = await db.SecurityTokens
            .Where(item =>
                item.UserId == user.Id
                && item.Purpose == SecurityTokenPurpose.EmailConfirmation
                && item.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in previous)
        {
            token.Invalidate(now);
        }

        var plainToken = SecurityTokenCodec.CreatePlain();
        db.SecurityTokens.Add(SecurityToken.Issue(
            user.Id,
            SecurityTokenPurpose.EmailConfirmation,
            SecurityTokenCodec.Hash(plainToken),
            now,
            EmailConfirmationPolicy.TokenLifetime));

        await db.SaveChangesAsync(cancellationToken);

        var (subject, body) = EmailConfirmationMail.Compose(user.UserName.Value, links.EmailConfirmation(plainToken));
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
            return Result.Failure(IdentityErrors.EmailSendFailed());
        }

        return Result.Success();
    }
}
