namespace Situ.Domain.Identity;

/// <summary>How long a session credential stays valid. The access token is short; the refresh token rotates.</summary>
public static class SessionPolicy
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
}
