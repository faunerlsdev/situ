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
    }

    public static bool IsFullSession(System.Security.Claims.ClaimsPrincipal user)
    {
        var scope = user.FindFirst("scope")?.Value;
        return scope is not (GuardianConsentScope or DeviceManagementScope);
    }

    public static bool CanManageDevices(System.Security.Claims.ClaimsPrincipal user)
    {
        var scope = user.FindFirst("scope")?.Value;
        return scope is null || scope == DeviceManagementScope;
    }
}
