using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Situ.Api.Auth;

namespace Situ.Architecture.Tests;

public class GuardianConsentScopeTests
{
    [Fact]
    public async Task A_guardian_consent_token_is_rejected_by_the_normal_user_policy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.User, policy =>
                policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    context.User.FindFirst("scope")?.Value is not AuthorizationPolicies.GuardianConsentScope));
            options.AddPolicy(AuthorizationPolicies.GuardianConsent, policy =>
                policy.RequireAuthenticatedUser().RequireClaim("scope", AuthorizationPolicies.GuardianConsentScope));
        });
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("scope", AuthorizationPolicies.GuardianConsentScope)],
            authenticationType: "Bearer"));

        Assert.False((await authorization.AuthorizeAsync(principal, AuthorizationPolicies.User)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(principal, AuthorizationPolicies.GuardianConsent)).Succeeded);
    }
}
