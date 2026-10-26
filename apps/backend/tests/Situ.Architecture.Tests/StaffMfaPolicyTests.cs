using System.Security.Claims;
using Situ.Api.Auth;

namespace Situ.Architecture.Tests;

public class StaffMfaPolicyTests
{
    [Fact]
    public void An_enrollment_token_cannot_open_the_admin_panel()
    {
        var enrollment = Principal(new Claim("scope", AuthorizationPolicies.MfaEnrollmentScope));
        var challenge = Principal(new Claim("scope", AuthorizationPolicies.MfaChallengeScope));
        var staffWithoutMfa = Principal(new Claim("role", AuthorizationPolicies.Staff));
        var completed = Principal(new Claim("role", AuthorizationPolicies.Staff), new Claim("scope", AuthorizationPolicies.MfaScope));

        Assert.False(AuthorizationPolicies.CanUseAdmin(enrollment));
        Assert.False(AuthorizationPolicies.CanUseAdmin(challenge));
        Assert.False(AuthorizationPolicies.CanUseAdmin(staffWithoutMfa));
        Assert.True(AuthorizationPolicies.CanUseAdmin(completed));
        Assert.False(AuthorizationPolicies.IsFullSession(completed));
        Assert.False(AuthorizationPolicies.IsFullSession(enrollment));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer", nameType: "sub", roleType: "role"));
}
