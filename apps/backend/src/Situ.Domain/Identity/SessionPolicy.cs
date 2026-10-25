namespace Situ.Domain.Identity;

/// <summary>How long a session credential stays valid. The access token is short; the refresh token rotates.</summary>
public static class SessionPolicy
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>Panel access token. Shorter than <see cref="AccessTokenLifetime"/>.</summary>
    public static readonly TimeSpan StaffAccessTokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Idle window of a panel session. Each refresh starts this window again.</summary>
    public static readonly TimeSpan StaffIdleLifetime = TimeSpan.FromMinutes(30);

    public static TimeSpan AccessLifetimeFor(string? role) =>
        string.Equals(role, "Staff", StringComparison.Ordinal) ? StaffAccessTokenLifetime : AccessTokenLifetime;

    public static TimeSpan RefreshLifetimeFor(string? role) =>
        string.Equals(role, "Staff", StringComparison.Ordinal) ? StaffIdleLifetime : RefreshTokenLifetime;
}
