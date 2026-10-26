using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Situ.Api.Auth;
using Situ.Api.Realtime;

namespace Situ.Architecture.Tests;

public class MapHubAuthTests
{
    [Theory]
    [InlineData("/hubs/map", "token", true)]
    [InlineData("/hubs/map/negotiate", "token", true)]
    [InlineData("/api/v1/reports/map", "token", false)]
    [InlineData("/hubs/map", "", false)]
    public void The_access_token_query_is_read_only_for_the_map_hub(string path, string token, bool expected)
    {
        Assert.Equal(expected, MapHubAuth.ReadAccessTokenFromQuery(path, token));
    }

    [Fact]
    public void The_hub_requires_a_full_user_session()
    {
        var authorize = typeof(MapHub).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(AuthorizationPolicies.User, authorize.Policy);
    }
}
