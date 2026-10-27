namespace Situ.Api.Realtime;

public static class MapHubAuth
{
    public const string HubPath = "/hubs/map";
    public const string FriendsHubPath = "/hubs/friends";

    /// <summary>The access token query is read only on the realtime hubs. Every other route keeps the Authorization header.</summary>
    public static bool ReadAccessTokenFromQuery(PathString path, string? accessToken) =>
        !string.IsNullOrEmpty(accessToken) && IsRealtimeHub(path);

    /// <summary>The one-time realtime ticket is read only on the realtime hubs. It is not a session on any other route.</summary>
    public static bool ReadTicketFromQuery(PathString path, string? ticket) =>
        !string.IsNullOrEmpty(ticket) && IsRealtimeHub(path);

    private static bool IsRealtimeHub(PathString path) =>
        path.StartsWithSegments(HubPath) || path.StartsWithSegments(FriendsHubPath);
}
