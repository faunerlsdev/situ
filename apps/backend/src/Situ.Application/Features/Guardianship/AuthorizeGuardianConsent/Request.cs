using Situ.Domain.Identity;

namespace Situ.Application.Features.Guardianship.AuthorizeGuardianConsent;

public sealed record AuthorizeGuardianConsentRequest(
    string Token,
    bool LiveStreamingAuthorized,
    bool LocationSharingAuthorized,
    string? IpAddress,
    string? UserAgent);

public sealed record AuthorizeGuardianConsentResponse(Guid UserId, UserStatus Status);
