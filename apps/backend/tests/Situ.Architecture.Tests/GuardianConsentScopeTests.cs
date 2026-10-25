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
        services.AddAuthorization(AuthorizationPolicies.Add);
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("scope", AuthorizationPolicies.GuardianConsentScope)],
            authenticationType: "Bearer"));

        Assert.False((await authorization.AuthorizeAsync(principal, AuthorizationPolicies.User)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, AuthorizationPolicies.DeviceSession)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(principal, AuthorizationPolicies.GuardianConsent)).Succeeded);
    }

    [Fact]
    public async Task A_device_management_token_can_list_devices_and_cannot_use_the_rest_of_the_account()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AuthorizationPolicies.Add);
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("scope", AuthorizationPolicies.DeviceManagementScope)],
            authenticationType: "Bearer"));

        Assert.False((await authorization.AuthorizeAsync(principal, AuthorizationPolicies.User)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(principal, AuthorizationPolicies.DeviceSession)).Succeeded);
    }
}
