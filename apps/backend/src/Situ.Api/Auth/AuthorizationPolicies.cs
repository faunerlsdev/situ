namespace Situ.Api.Auth;

public static class AuthorizationPolicies
{
    public const string User = "User";

    /// <summary>Short-lived token that can only use the guardian-consent flow.</summary>
    public const string GuardianConsent = "GuardianConsent";

    public const string GuardianConsentScope = "guardian_consent";

    /// <summary>Requires an authenticated token whose <c>role</c> claim is <c>Staff</c>.</summary>
    public const string Staff = "Staff";
}
