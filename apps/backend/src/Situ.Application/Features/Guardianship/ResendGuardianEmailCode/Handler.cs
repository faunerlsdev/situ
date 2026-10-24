using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.ResendGuardianEmailCode;

public sealed record ResendGuardianEmailCodeRequest(string Token);

public sealed class ResendGuardianEmailCodeHandler(
    IApplicationDbContext db,
    IClock clock,
    IEmailSender email)
    : IHandler<ResendGuardianEmailCodeRequest, Result>
{
    public async Task<Result> Handle(ResendGuardianEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var consent = await GuardianConsentLookup.ByReviewTokenAsync(db, request.Token, cancellationToken);
        if (consent is null || !consent.IsOpen(now) || consent.GuardianContactEmail is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        var rules = await GuardianLimitRules.LoadAsync(db, cancellationToken);
        var since = now.AddMinutes(-15);
        var recent = await db.SecurityTokens.CountAsync(
            item => item.UserId == consent.MinorUserId
                && item.Purpose == SecurityTokenPurpose.GuardianEmailCode
                && item.CreatedAt >= since,
            cancellationToken);
        if (recent >= rules.MaxCodeResendsPer15Minutes + 1)
        {
            return Result.Failure(GuardianErrors.SendLimit(since.AddMinutes(15)));
        }

        var previous = await db.SecurityTokens
            .Where(item => item.UserId == consent.MinorUserId
                && item.Purpose == SecurityTokenPurpose.GuardianEmailCode
                && item.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in previous)
        {
            token.Invalidate(now);
        }

        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.SecurityTokens.Add(SecurityToken.Issue(
            consent.MinorUserId,
            SecurityTokenPurpose.GuardianEmailCode,
            SecurityTokenCodec.Hash(code),
            now,
            MinorProtectionPolicy.GuardianEmailCodeLifetime));
        await db.SaveChangesAsync(cancellationToken);

        var (subject, body) = GuardianConsentMail.VerificationCode(code);
        try
        {
            await email.SendAsync(consent.GuardianContactEmail, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result.Failure(GuardianErrors.EmailSendFailed());
        }

        return Result.Success();
    }
}
