using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Guardianship.GetGuardianConsentForReview;

public sealed record GetGuardianConsentForReviewRequest(string? Token);

public sealed record GuardianConsentReviewResponse(
    string UserName,
    int Age,
    string City,
    string? MinorStatement,
    string NoticeVersion,
    DateTimeOffset ExpiresAt,
    bool RequiresLiveAuthorization);

public sealed class GetGuardianConsentForReviewHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetGuardianConsentForReviewRequest, Result<GuardianConsentReviewResponse>>
{
    public async Task<Result<GuardianConsentReviewResponse>> Handle(
        GetGuardianConsentForReviewRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var consent = await GuardianConsentLookup.ByReviewTokenAsync(db, request.Token, cancellationToken);
        if (consent is null || !consent.IsOpen(now))
        {
            return Result<GuardianConsentReviewResponse>.Failure(GuardianErrors.NotFound());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == consent.MinorUserId, cancellationToken);
        if (user is null || user.Status == Domain.Identity.UserStatus.Deleted)
        {
            return Result<GuardianConsentReviewResponse>.Failure(GuardianErrors.NotFound());
        }

        var city = await db.Cities.Where(item => item.Id == user.CityId).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        var age = user.AgeOn(now);
        return Result<GuardianConsentReviewResponse>.Success(new GuardianConsentReviewResponse(
            user.UserName.Value,
            age,
            string.IsNullOrWhiteSpace(city) ? "No disponible" : city,
            consent.MinorStatement,
            consent.NoticeVersion,
            consent.ExpiresAt,
            Domain.Guardianship.MinorProtectionPolicy.RequiresGuardianAuthorizationToStream(age)));
    }
}
