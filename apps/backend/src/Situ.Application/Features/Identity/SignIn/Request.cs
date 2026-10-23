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
    string? UserAgent);

public sealed record SignInResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid DeviceId);
