using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Situ.Api.Configuration;

namespace Situ.Architecture.Tests;

public class NetworkKnownProxiesTests
{
    [Fact]
    public void Development_starts_with_an_empty_proxy_list_and_does_not_trust_loopback()
    {
        using var host = BuildHost(Environments.Development, proxies: null);
        host.Start();

        var forwarded = host.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Empty(forwarded.KnownProxies);
        Assert.Empty(forwarded.KnownIPNetworks);
        Assert.Equal(1, forwarded.ForwardLimit);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, forwarded.ForwardedHeaders);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Non_development_fails_at_startup_without_known_proxies(string environment)
    {
        var missing = Record.Exception(() =>
        {
            using var host = BuildHost(environment, proxies: null);
            host.Start();
        });
        var invalid = Record.Exception(() =>
        {
            using var host = BuildHost(environment, proxies: ["no-es-ip"]);
            host.Start();
        });

        Assert.NotNull(missing);
        Assert.Contains(NetworkRules.MissingProxiesMessage, missing.ToString());
        Assert.NotNull(invalid);
        Assert.Contains(NetworkRules.InvalidProxyMessage, invalid.ToString());

        using var ready = BuildHost(environment, proxies: ["10.4.4.4"]);
        ready.Start();
        var forwarded = ready.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Contains(forwarded.KnownProxies, address => address.Equals(IPAddress.Parse("10.4.4.4")));
        Assert.DoesNotContain(forwarded.KnownProxies, IPAddress.IsLoopback);
    }

    [Fact]
    public async Task A_spoofed_forwarded_for_is_ignored_unless_the_peer_is_a_known_proxy()
    {
        var ignored = await ReadClient("10.9.9.9", knownProxies: ["10.1.1.1"]);
        var trusted = await ReadClient("10.1.1.1", knownProxies: ["10.1.1.1"]);
        var development = await ReadClient("10.1.1.1", knownProxies: []);

        Assert.Equal("10.9.9.9|http", ignored);
        Assert.Equal("203.0.113.50|https", trusted);
        Assert.Equal("10.1.1.1|http", development);
    }

    [Fact]
    public void Console_commands_are_not_http_routes()
    {
        var root = RepoRoot();
        var endpoints = Directory.GetFiles(Path.Combine(root, "apps", "backend", "src", "Situ.Api", "Endpoints"), "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(endpoints);
        foreach (var file in endpoints)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("reset-2fa", text, StringComparison.Ordinal);
            Assert.DoesNotContain("dev:seed", text, StringComparison.Ordinal);
        }

        var docs = File.ReadAllText(Path.Combine(root, "docs", "admin.md"));
        Assert.Contains("procedimiento de emergencia", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("staff.2fa_reset_console", docs, StringComparison.Ordinal);
        Assert.Contains("Network:KnownProxies", docs, StringComparison.Ordinal);
        Assert.Contains("dev:seed", docs, StringComparison.Ordinal);
        Assert.Contains(DevSeedPassword(), docs, StringComparison.Ordinal);
    }

    private static string DevSeedPassword() => "Semilla123";

    private static async Task<string> ReadClient(string peer, string[] knownProxies)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = "Situ.Tests",
            ContentRootPath = AppContext.BaseDirectory,
            Args = [],
        });
        builder.WebHost.UseTestServer();
        builder.Services.Configure<NetworkOptions>(options => options.KnownProxies = knownProxies);
        builder.Services.Configure<ForwardedHeadersOptions>(options => ForwardedClient.Apply(options, knownProxies));
        var app = builder.Build();
        ForwardedClient.Use(app);
        app.MapGet("/who", (HttpContext http) => $"{http.Connection.RemoteIpAddress}|{http.Request.Scheme}");
        await app.StartAsync();
        try
        {
            var body = new MemoryStream();
            await app.GetTestServer().SendAsync(http =>
            {
                http.Response.Body = body;
                http.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                http.Request.Headers["X-Forwarded-For"] = "203.0.113.50";
                http.Request.Headers["X-Forwarded-Proto"] = "https";
                http.Request.Scheme = "http";
                http.Request.Method = HttpMethods.Get;
                http.Request.Path = "/who";
            });
            body.Position = 0;
            using var reader = new StreamReader(body);
            return await reader.ReadToEndAsync();
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static IHost BuildHost(string environment, string[]? proxies)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            ApplicationName = "Situ.Tests",
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=situ;Username=situ",
            ["Jwt:Issuer"] = "situ-api",
            ["Jwt:Audience"] = "situ-clients",
            ["Jwt:Secret"] = new string('j', 32),
            ["GuardianConsent:SigningKey"] = new string('k', 32),
        };
        if (environment == Environments.Production)
        {
            ProductionTestConfiguration.Apply(values);
        }

        if (proxies is not null)
        {
            for (var index = 0; index < proxies.Length; index++)
            {
                values[$"Network:KnownProxies:{index}"] = proxies[index];
            }
        }

        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddSituConfiguration(builder.Configuration);
        return builder.Build();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "admin.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
