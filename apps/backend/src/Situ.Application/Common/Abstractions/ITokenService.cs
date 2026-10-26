namespace Situ.Application.Common.Abstractions;

/// <summary>
/// Issues a JWT access token and an opaque refresh token.
/// Only the hash of the refresh token is stored (<see cref="Situ.Domain.Identity.RefreshToken"/>).
/// </summary>
public interface ITokenService
{
    string CreateAccessToken(Guid userId, DateTimeOffset expiresAt, string? scope = null, string? role = null, Guid? deviceId = null);

    string CreateRefreshToken();

    string Hash(string plainToken);

    /// <summary>Validates a previously issued access token. Returns null when the signature or lifetime fails.</summary>
    AccessTokenTicket? ReadAccessToken(string token) => null;
}

public sealed record AccessTokenTicket(Guid UserId, string? Scope, string? Role, DateTimeOffset ExpiresAt);
