namespace Situ.Api.Auth;

public static class CorsPolicies
{
    public const string Clients = "Clients";

    public static bool IsAllowedOrigin(string origin, IReadOnlyList<string>? configured, bool isDevelopment)
    {
        if (configured is not null)
        {
            foreach (var allowed in configured)
            {
                if (string.Equals(allowed.Trim().TrimEnd('/'), origin.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (!isDevelopment || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Host is not ("localhost" or "127.0.0.1" or "192.168.1.119"))
        {
            return false;
        }

        return uri.Port is 3000 or 3001 or 3002 or 3003 or 3004 or 8081 or 19000 or 19006;
    }
}
