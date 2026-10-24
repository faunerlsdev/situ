using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.GetMyGuardianConsentStatus;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.ResendGuardianConsentEmail;

public sealed record ResendGuardianConsentEmailRequest(string? IpAddress);

public sealed class ResendGuardianConsentEmailHandler(
    IApplicationDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IEmailSender email,
    IPublicLinks links)
    : IHandler<ResendGuardianConsentEmailRequest, Result<GuardianConsentStatusResponse>>
{
    public async Task<Result<GuardianConsentStatusResponse>> Handle(
        ResendGuardianConsentEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<GuardianConsentStatusResponse>.Failure(AuthErrors.Unauthenticated());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status != UserStatus.PendingGuardianConsent)
        {
            return Result<GuardianConsentStatusResponse>.Failure(GuardianErrors.NotPending());
        }

        var now = clock.UtcNow;
        var consent = await db.GuardianConsents
            .Where(item => item.MinorUserId == userId)
            .OrderByDescending(item => item.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (consent is null || !consent.IsOpen(now))
        {
            return Result<GuardianConsentStatusResponse>.Failure(GuardianErrors.NoOpenRequest());
        }

        var blocked = await GuardianDispatchGate.RejectionAsync(
            db, userId, consent.GuardianEmail, request.IpAddress, false, now, cancellationToken);
        if (blocked is not null)
        {
            return Result<GuardianConsentStatusResponse>.Failure(blocked);
        }

        var plain = SecurityTokenCodec.CreatePlain();
        try
        {
            consent.ReplaceLink(SecurityTokenCodec.Hash(plain), now);
        }
        catch (DomainException exception)
        {
            return Result<GuardianConsentStatusResponse>.Failure(GuardianErrors.FromDomain(exception));
        }

        db.GuardianConsentDispatches.Add(GuardianConsentDispatch.Record(
            userId, consent.GuardianEmail, request.IpAddress, GuardianDispatchKind.Resend, now));
        await db.SaveChangesAsync(cancellationToken);
        await TrySendAsync(email, consent.GuardianEmail, links.GuardianReview(plain), cancellationToken);
        return await new GetMyGuardianConsentStatusHandler(db, clock, currentUser)
            .Handle(new GetMyGuardianConsentStatusRequest(), cancellationToken);
    }

    internal static async Task TrySendAsync(IEmailSender email, string to, string url, CancellationToken cancellationToken)
    {
        var (subject, body) = GuardianConsentMail.Request(url);
        try
        {
            await email.SendAsync(to, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The new link is already stored. A later resend can try again.
        }
    }
}
