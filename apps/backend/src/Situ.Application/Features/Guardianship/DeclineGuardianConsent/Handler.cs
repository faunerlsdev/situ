using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.RevokeGuardianConsent;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.DeclineGuardianConsent;

public sealed class DeclineGuardianConsentHandler(IApplicationDbContext db, IClock clock, IEmailSender email)
    : IHandler<DeclineGuardianConsentRequest, Result>
{
    public async Task<Result> Handle(DeclineGuardianConsentRequest request, CancellationToken cancellationToken)
    {
        var consent = await GuardianConsentLookup.ByReviewTokenAsync(db, request.Token, cancellationToken);
        if (consent is null)
        {
            return Result.Failure(GuardianErrors.NotFound());
        }

        var now = clock.UtcNow;
        try
        {
            consent.Decline(now, request.Reason);
        }
        catch (DomainException exception)
        {
            return Result.Failure(GuardianErrors.FromDomain(exception));
        }

        await db.SaveChangesAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == consent.MinorUserId, cancellationToken);
        if (user is not null && user.Status != UserStatus.Deleted)
        {
            var notice = GuardianConsentMail.MinorDeclined();
            await RevokeGuardianConsentHandler.TrySendAsync(email, user.Email.Value, notice.Subject, notice.Body, cancellationToken);
        }

        return Result.Success();
    }
}
