namespace Situ.Api.Realtime;

public static class MapHubAuth
{
    public const string HubPath = "/hubs/map";

    /// <summary>The access token query is read only on the map hub. Every other route keeps the Authorization header.</summary>
    public static bool ReadAccessTokenFromQuery(PathString path, string? accessToken) =>
        !string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(HubPath);

    /// <summary>The one-time realtime ticket is read only on the map hub. It is not a session on any other route.</summary>
    public static bool ReadTicketFromQuery(PathString path, string? ticket) =>
        !string.IsNullOrEmpty(ticket) && path.StartsWithSegments(HubPath);
}
