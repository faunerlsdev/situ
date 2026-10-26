using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Situ.Api.Configuration;

public sealed class NetworkOptions
{
    public const string SectionName = "Network";

    public string[] KnownProxies { get; set; } = [];
}

public static class NetworkRules
{
    public const string MissingProxiesMessage =
        "Falta Network:KnownProxies. Fuera de Development la API solo acepta X-Forwarded-For desde proxies de confianza. Define Network__KnownProxies__0 con la IP del proxy.";

    public const string InvalidProxyMessage =
        "Network:KnownProxies tiene una dirección que no es una IP.";

    public static bool TryValidate(IReadOnlyList<string>? proxies, bool isDevelopment, out string? error)
    {
        proxies ??= [];
        foreach (var proxy in proxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                error = InvalidProxyMessage;
                return false;
            }
        }

        if (!isDevelopment && proxies.Count == 0)
        {
            error = MissingProxiesMessage;
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class NetworkOptionsValidator(IHostEnvironment environment) : IValidateOptions<NetworkOptions>
{
    public ValidateOptionsResult Validate(string? name, NetworkOptions options)
    {
        if (NetworkRules.TryValidate(options.KnownProxies, environment.IsDevelopment(), out var error))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(error!);
    }
}

/// <summary>
/// Trusts X-Forwarded-For and X-Forwarded-Proto only from <see cref="NetworkOptions.KnownProxies"/>.
/// The framework treats an empty list as "trust every caller", so the middleware is not installed then.
/// </summary>
public static class ForwardedClient
{
    public static void Apply(ForwardedHeadersOptions options, IReadOnlyList<string>? knownProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        options.ForwardLimit = 1;
        foreach (var proxy in knownProxies ?? [])
        {
            var address = IPAddress.Parse(proxy);
            if (!options.KnownProxies.Contains(address))
            {
                options.KnownProxies.Add(address);
            }
        }
    }

    public static void Use(IApplicationBuilder app)
    {
        var proxies = app.ApplicationServices.GetRequiredService<IOptions<NetworkOptions>>().Value.KnownProxies;
        if (proxies is { Length: > 0 })
        {
            app.UseForwardedHeaders();
        }
    }
}
