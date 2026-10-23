namespace Situ.Application.Common.Abstractions;

/// <summary>
/// Issues a JWT access token and an opaque refresh token.
/// Only the hash of the refresh token is stored (<see cref="Situ.Domain.Identity.RefreshToken"/>).
/// </summary>
public interface ITokenService
{
    string CreateAccessToken(Guid userId, DateTimeOffset expiresAt);

    string CreateRefreshToken();

    string Hash(string plainToken);
}
