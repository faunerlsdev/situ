using Microsoft.AspNetCore.Authorization;

namespace Situ.Api.Auth;

public static class AuthorizationPolicies
{
    public const string User = "User";

    /// <summary>Short-lived token that can only use the guardian-consent flow.</summary>
    public const string GuardianConsent = "GuardianConsent";

    public const string GuardianConsentScope = "guardian_consent";

    /// <summary>Full session, or the short-lived token issued when sign-in hits the device limit.</summary>
    public const string DeviceSession = "DeviceSession";

    public const string DeviceManagementScope = "device_management";

    /// <summary>Requires an authenticated token whose <c>role</c> claim is <c>Staff</c>.</summary>
    public const string Staff = "Staff";

    /// <summary>Staff session that already completed TOTP.</summary>
    public const string StaffMfa = "StaffMfa";

    /// <summary>Short-lived token that can only enroll an authenticator.</summary>
    public const string MfaEnrollment = "MfaEnrollment";

    public const string MfaScope = "mfa";

    public const string MfaEnrollmentScope = "mfa_enrollment";

    public const string MfaChallengeScope = "mfa_challenge";

    public static void Add(AuthorizationOptions options)
    {
        options.AddPolicy(User, policy =>
            policy.RequireAuthenticatedUser().RequireAssertion(context => IsFullSession(context.User)));
        options.AddPolicy(GuardianConsent, policy =>
            policy.RequireAuthenticatedUser().RequireClaim("scope", GuardianConsentScope));
        options.AddPolicy(DeviceSession, policy =>
            policy.RequireAuthenticatedUser().RequireAssertion(context => CanManageDevices(context.User)));
        options.AddPolicy(Staff, policy =>
            policy.RequireAuthenticatedUser().RequireRole(Staff));
        options.AddPolicy(StaffMfa, policy =>
            policy.RequireAuthenticatedUser().RequireRole(Staff).RequireClaim("scope", MfaScope));
        options.AddPolicy(MfaEnrollment, policy =>
            policy.RequireAuthenticatedUser().RequireClaim("scope", MfaEnrollmentScope));
    }

    public static bool IsFullSession(System.Security.Claims.ClaimsPrincipal user)
    {
        var scope = user.FindFirst("scope")?.Value;
        return scope is not (GuardianConsentScope or DeviceManagementScope or MfaScope or MfaEnrollmentScope or MfaChallengeScope);
    }

    public static bool CanUseAdmin(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Staff) && user.HasClaim("scope", MfaScope);

    public static bool CanManageDevices(System.Security.Claims.ClaimsPrincipal user)
    {
        var scope = user.FindFirst("scope")?.Value;
        return scope is null || scope == DeviceManagementScope;
    }
}
