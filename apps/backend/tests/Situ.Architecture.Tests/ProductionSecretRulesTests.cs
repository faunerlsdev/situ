using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Situ.Api.Configuration;
using Situ.Api.Health;
using Situ.Infrastructure.Guardianship;
using Situ.Infrastructure.Reports;

namespace Situ.Architecture.Tests;

public class ProductionSecretRulesTests
{
    [Fact]
    public void Production_accepts_distinct_secrets_and_rejects_placeholders_and_development_keys()
    {
        Assert.True(ProductionSecretRules.TryValidate(Config(Valid()), out _));

        AssertRejected(Valid(), "Jwt:Secret", GuardianConsentOptions.DevelopmentSigningKey, ProductionSecretRules.JwtRejectedMessage);
        AssertRejected(Valid(), "Jwt:Secret", "CHANGE_ME_this_is_long_enough_for_jwt", ProductionSecretRules.JwtRejectedMessage);
        AssertRejected(Valid(), "GuardianConsent:SigningKey", GuardianConsentOptions.DevelopmentSigningKey, ProductionSecretRules.GuardianRejectedMessage);
        AssertRejected(Valid(), "FileStorage:SigningKey", FileStorageOptions.DevelopmentSigningKey, ProductionSecretRules.MediaRejectedMessage);
        AssertRejected(Valid(), "FileStorage:SigningKey", "", ProductionSecretRules.MediaRejectedMessage);
        AssertRejected(Valid(), "Notifications:ExpoAccessToken", "placeholder-token-value", ProductionSecretRules.ExpoRejectedMessage);
        AssertRejected(Valid(), "Captcha:Provider", "Development", ProductionSecretRules.CaptchaRejectedMessage);
        AssertRejected(Valid(), "PublicBaseUrl", "http://localhost:5080", ProductionSecretRules.PublicUrlMessage);
        AssertRejected(Valid(), "Email:Host", "localhost", ProductionSecretRules.EmailHostMessage);
        AssertRejected(Valid(), "Email:Password", "CHANGE_ME_password", ProductionSecretRules.EmailPasswordMessage);
        AssertRejected(Valid(), "ConnectionStrings:Default", "Host=localhost;Password=TU_CLAVE", ProductionSecretRules.ConnectionRejectedMessage);
    }

    [Fact]
    public void Production_host_fails_to_start_when_a_secret_matches_the_development_key()
    {
        var values = Valid();
        values["FileStorage:SigningKey"] = FileStorageOptions.DevelopmentSigningKey;
        var exception = Record.Exception(() =>
        {
            using var host = BuildHost(values);
            host.Start();
        });

        Assert.NotNull(exception);
        Assert.Contains(ProductionSecretRules.MediaRejectedMessage, exception.ToString());
    }

    [Fact]
    public void Production_host_starts_when_the_secrets_are_valid()
    {
        using var host = BuildHost(Valid());
        host.Start();
        Assert.True(ProductionSecretRules.TryValidate(host.Services.GetRequiredService<IConfiguration>(), out _));
    }

    [Fact]
    public void Development_does_not_apply_the_production_gate()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development,
            ApplicationName = "Situ.Tests",
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=situ;Username=situ",
            ["Jwt:Secret"] = new string('j', 32),
        });
        builder.Services.AddSituConfiguration(builder.Configuration);
        using var host = builder.Build();
        host.Start();
    }

    [Fact]
    public async Task Storage_health_stays_free_of_paths_and_file_contents()
    {
        var root = Path.Combine(Path.GetTempPath(), "situ-health-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var healthy = await new StorageHealthCheck(new FileStorageOptions { Root = root, SigningKey = "ignored" })
                .CheckHealthAsync(new HealthCheckContext());
            Assert.Equal(HealthStatus.Healthy, healthy.Status);
            Assert.Null(healthy.Description);
            Assert.Empty(healthy.Data);
            Assert.False(File.Exists(Path.Combine(root, ".health")));

            var missing = await new StorageHealthCheck(new FileStorageOptions { Root = "", SigningKey = "ignored" })
                .CheckHealthAsync(new HealthCheckContext());
            Assert.Equal(HealthStatus.Unhealthy, missing.Status);
            Assert.Null(missing.Description);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertRejected(Dictionary<string, string?> values, string key, string? replacement, string message)
    {
        var copy = new Dictionary<string, string?>(values) { [key] = replacement };
        Assert.False(ProductionSecretRules.TryValidate(Config(copy), out var error));
        Assert.Contains(message, error);
    }

    private static Dictionary<string, string?> Valid()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=127.0.0.1;Database=situ;Username=situ_app;Password=db-credential-for-tests",
            ["Jwt:Secret"] = "jwt-signing-key-for-tests-32b-minimum",
            ["GuardianConsent:SigningKey"] = "guardian-signing-key-for-tests-32b",
            ["Network:KnownProxies:0"] = "127.0.0.1",
        };
        ProductionTestConfiguration.Apply(values);
        return values;
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHost BuildHost(Dictionary<string, string?> values)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
            ApplicationName = "Situ.Tests",
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true,
        });
        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddSituConfiguration(builder.Configuration);
        return builder.Build();
    }
}
