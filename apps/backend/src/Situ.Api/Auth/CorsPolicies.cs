namespace Situ.Api.Auth;

public static class CorsPolicies
{
    public const string Clients = "Clients";

    public static bool IsAllowedOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Host is not ("localhost" or "127.0.0.1"))
        {
            return false;
        }

        return uri.Port is 3000 or 3001 or 3002 or 8081 or 19000 or 19006;
    }
}
