using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.GetMyGuardianConsentStatus;
using Situ.Application.Features.Identity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.ChangeGuardianEmail;

public sealed record ChangeGuardianEmailRequest(string GuardianEmail, string? IpAddress);

public sealed class ChangeGuardianEmailHandler(
    IApplicationDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    IEmailSender email,
    IPublicLinks links)
    : IHandler<ChangeGuardianEmailRequest, Result<GuardianConsentStatusResponse>>
{
    public async Task<Result<GuardianConsentStatusResponse>> Handle(
        ChangeGuardianEmailRequest request,
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

        if (!Email.TryCreate(request.GuardianEmail, out var parsed) || parsed.Value.Equals(user.Email.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Result<GuardianConsentStatusResponse>.Failure(GuardianErrors.GuardianEmailRejected());
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
            db, userId, parsed.Value, request.IpAddress, true, now, cancellationToken);
        if (blocked is not null)
        {
            return Result<GuardianConsentStatusResponse>.Failure(blocked);
        }

        var plain = SecurityTokenCodec.CreatePlain();
        try
        {
            consent.ChangeGuardianEmail(parsed.Value, SecurityTokenCodec.Hash(plain), now);
        }
        catch (DomainException exception)
        {
            return Result<GuardianConsentStatusResponse>.Failure(GuardianErrors.FromDomain(exception));
        }

        db.GuardianConsentDispatches.Add(GuardianConsentDispatch.Record(
            userId, parsed.Value, request.IpAddress, GuardianDispatchKind.EmailChange, now));
        await db.SaveChangesAsync(cancellationToken);
        await ResendGuardianConsentEmail.ResendGuardianConsentEmailHandler.TrySendAsync(
            email, parsed.Value, links.GuardianReview(plain), cancellationToken);
        return await new GetMyGuardianConsentStatusHandler(db, clock, currentUser)
            .Handle(new GetMyGuardianConsentStatusRequest(), cancellationToken);
    }
}
