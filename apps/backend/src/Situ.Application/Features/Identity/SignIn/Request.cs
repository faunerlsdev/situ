using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.SignIn;

public sealed record SignInRequest(
    string Login,
    string Password,
    string DeviceIdentifier,
    DevicePlatform Platform,
    string? DeviceName,
    string? AppVersion,
    string? PushToken,
    string? IpAddress,
    string? UserAgent,
    bool RememberMe = false);

public sealed record SignInResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid DeviceId,
    DateTimeOffset RefreshTokenExpiresAt);
