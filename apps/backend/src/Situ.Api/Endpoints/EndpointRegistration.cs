using System.Reflection;

namespace Situ.Api.Endpoints;

public static class EndpointRegistration
{
    public static WebApplication MapSituEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1");
        var mapParameters = new object[] { group };

        foreach (var type in typeof(EndpointRegistration).Assembly.GetTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (type is not { IsAbstract: false, IsInterface: false } || !typeof(IEndpoint).IsAssignableFrom(type))
            {
                continue;
            }

            var map = type.GetMethod(nameof(IEndpoint.Map), BindingFlags.Public | BindingFlags.Static, [typeof(RouteGroupBuilder)]);
            map?.Invoke(null, mapParameters);
        }

        return app;
    }
}
