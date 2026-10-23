using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.SubmitGuardianData;

public sealed class SubmitGuardianDataHandler(
    IApplicationDbContext db,
    IClock clock,
    IEmailSender email,
    IPublicLinks links)
    : IHandler<SubmitGuardianDataRequest, Result>
{
    public async Task<Result> Handle(SubmitGuardianDataRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var consent = await FindConsentAsync(request.Token, cancellationToken);
        if (consent is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        try
        {
            consent.SubmitGuardianData(
                request.FullName.Trim(),
                request.DocumentType,
                request.DocumentNumber.Trim(),
                request.ContactEmail,
                now);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        var previous = await db.SecurityTokens
            .Where(item =>
                item.UserId == consent.MinorUserId
                && item.Purpose == SecurityTokenPurpose.GuardianEmailVerification
                && item.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in previous)
        {
            token.Invalidate(now);
        }

        var plainToken = SecurityTokenCodec.CreatePlain();
        db.SecurityTokens.Add(SecurityToken.Issue(
            consent.MinorUserId,
            SecurityTokenPurpose.GuardianEmailVerification,
            SecurityTokenCodec.Hash(plainToken),
            now,
            MinorProtectionPolicy.GuardianEmailVerificationLifetime));
        await db.SaveChangesAsync(cancellationToken);

        var (subject, body) = GuardianConsentMail.Verification(
            consent.GuardianFullName!,
            links.GuardianEmailVerification(plainToken));
        try
        {
            await email.SendAsync(consent.GuardianContactEmail!, subject, body, cancellationToken);
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

    private async Task<GuardianConsent?> FindConsentAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = SecurityTokenCodec.Hash(token.Trim());
        return await db.GuardianConsents.FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
    }
}
