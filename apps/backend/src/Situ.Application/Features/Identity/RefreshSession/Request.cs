namespace Situ.Application.Features.Identity.RefreshSession;

public sealed record RefreshSessionRequest(string RefreshToken, string? IpAddress);

public sealed record RefreshSessionResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid DeviceId);
