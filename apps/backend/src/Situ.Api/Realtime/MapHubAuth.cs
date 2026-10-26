namespace Situ.Api.Realtime;

public static class MapHubAuth
{
    public const string HubPath = "/hubs/map";

    /// <summary>The access token query is read only on the map hub. Every other route keeps the Authorization header.</summary>
    public static bool ReadAccessTokenFromQuery(PathString path, string? accessToken) =>
        !string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(HubPath);
}
