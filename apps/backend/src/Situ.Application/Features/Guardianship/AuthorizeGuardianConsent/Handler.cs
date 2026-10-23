using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.AuthorizeGuardianConsent;

public sealed class AuthorizeGuardianConsentHandler(
    IApplicationDbContext db,
    IClock clock,
    IGuardianConsentSigner signer,
    IEmailSender email)
    : IHandler<AuthorizeGuardianConsentRequest, Result<AuthorizeGuardianConsentResponse>>
{
    public async Task<Result<AuthorizeGuardianConsentResponse>> Handle(
        AuthorizeGuardianConsentRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var consent = await FindConsentAsync(request.Token, cancellationToken);
        if (consent is null)
        {
            return Result<AuthorizeGuardianConsentResponse>.Failure(GuardianErrors.NotFound());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == consent.MinorUserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return Result<AuthorizeGuardianConsentResponse>.Failure(GuardianErrors.NotFound());
        }

        string signature;
        try
        {
            signature = signer.Sign(new GuardianConsentSignature(
                consent.Id,
                consent.MinorUserId,
                consent.NoticeVersion,
                consent.GuardianFullName ?? "",
                consent.GuardianDocumentType?.ToString() ?? "",
                consent.GuardianDocumentNumber ?? "",
                consent.GuardianContactEmail ?? "",
                request.LiveStreamingAuthorized,
                request.LocationSharingAuthorized,
                now));
            consent.Authorize(
                signature,
                request.LiveStreamingAuthorized,
                request.LocationSharingAuthorized,
                Clip(request.IpAddress, 64),
                Clip(request.UserAgent, 512),
                now);
        }
        catch (DomainException exception)
        {
            return Result<AuthorizeGuardianConsentResponse>.Failure(GuardianErrors.FromDomain(exception));
        }

        user.GrantGuardianConsent(now);
        await db.SaveChangesAsync(cancellationToken);

        var (subject, body) = GuardianConsentMail.Receipt(
            consent.GuardianFullName!,
            user.UserName.Value,
            GuardianConsentMail.DocumentLabel(consent.GuardianDocumentType!.Value),
            consent.GuardianDocumentNumber!,
            consent.GuardianContactEmail!,
            consent.LiveStreamingAuthorized,
            consent.LocationSharingAuthorized,
            consent.NoticeVersion,
            signature,
            now);
        try
        {
            await email.SendAsync(consent.GuardianContactEmail!, subject, body, cancellationToken);
            consent.MarkReceiptSent(now);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<AuthorizeGuardianConsentResponse>.Success(new AuthorizeGuardianConsentResponse(user.Id, user.Status));
        }

        return Result<AuthorizeGuardianConsentResponse>.Success(new AuthorizeGuardianConsentResponse(user.Id, user.Status));
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

    private static string? Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
