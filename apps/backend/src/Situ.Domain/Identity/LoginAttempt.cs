using Situ.Domain.Common;

namespace Situ.Domain.Identity;

/// <summary>Security audit trail of every sign-in attempt (successful or not).</summary>
public sealed class LoginAttempt : Entity
{
    private LoginAttempt()
    {
        EmailAttempted = null!;
    }

    public Guid? UserId { get; private set; }

    /// <summary>The e-mail typed by the person (kept even when no account matches).</summary>
    public string EmailAttempted { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceIdentifier { get; private set; }
    public bool Succeeded { get; private set; }
    public LoginFailureReason? FailureReason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static LoginAttempt Success(
        Guid userId, string email, string? ipAddress, string? userAgent, string? deviceIdentifier, DateTimeOffset now) => new()
    {
        UserId = userId,
        EmailAttempted = Guard.NotEmpty(email, nameof(email), 254),
        IpAddress = Guard.Optional(ipAddress, nameof(ipAddress), 64),
        UserAgent = Guard.Optional(userAgent, nameof(userAgent), 512),
        DeviceIdentifier = Guard.Optional(deviceIdentifier, nameof(deviceIdentifier), 128),
        Succeeded = true,
        OccurredAt = now,
    };

    public static LoginAttempt Failure(
        Guid? userId,
        string email,
        LoginFailureReason reason,
        string? ipAddress,
        string? userAgent,
        string? deviceIdentifier,
        DateTimeOffset now) => new()
    {
        UserId = userId,
        EmailAttempted = Guard.NotEmpty(email, nameof(email), 254),
        IpAddress = Guard.Optional(ipAddress, nameof(ipAddress), 64),
        UserAgent = Guard.Optional(userAgent, nameof(userAgent), 512),
        DeviceIdentifier = Guard.Optional(deviceIdentifier, nameof(deviceIdentifier), 128),
        Succeeded = false,
        FailureReason = reason,
        OccurredAt = now,
    };
}
