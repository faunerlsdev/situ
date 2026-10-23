namespace Situ.Api.Auth;

public static class AuthorizationPolicies
{
    public const string User = "User";

    /// <summary>Requires an authenticated token whose <c>role</c> claim is <c>Staff</c>.</summary>
    public const string Staff = "Staff";
}
