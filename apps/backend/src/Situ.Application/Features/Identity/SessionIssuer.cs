using Situ.Application.Common.Abstractions;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity;

internal static class SessionIssuer
{
    public static (string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, RefreshToken Stored) Issue(
        ITokenService tokens,
        Guid userId,
        Guid deviceId,
        DateTimeOffset now)
    {
        var accessExpiresAt = now.Add(SessionPolicy.AccessTokenLifetime);
        var plainRefresh = tokens.CreateRefreshToken();
        var stored = RefreshToken.Issue(
            userId,
            deviceId,
            tokens.Hash(plainRefresh),
            now,
            SessionPolicy.RefreshTokenLifetime);

        return (tokens.CreateAccessToken(userId, accessExpiresAt), accessExpiresAt, plainRefresh, stored);
    }
}
