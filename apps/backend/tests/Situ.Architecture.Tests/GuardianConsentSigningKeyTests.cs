using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Situ.Api.Configuration;
using Situ.Infrastructure.Guardianship;

namespace Situ.Architecture.Tests;

public class GuardianConsentSigningKeyTests
{
    [Fact]
    public void Development_uses_the_fixed_key_and_warns_that_signatures_are_not_valid()
    {
        Assert.True(Encoding.UTF8.GetByteCount(GuardianConsentOptions.DevelopmentSigningKey) >= 32);

        using var host = BuildHost(Environments.Development, signingKey: null, out var logs);
        host.Start();

        var options = host.Services.GetRequiredService<IOptions<GuardianConsentOptions>>().Value;
        Assert.Equal(GuardianConsentOptions.DevelopmentSigningKey, options.SigningKey);
        Assert.Contains(
            logs.Entries,
            entry => entry.Level == LogLevel.Warning && entry.Message == GuardianConsentOptions.DevelopmentSigningKeyWarning);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Non_development_fails_at_startup_when_the_key_is_missing_or_shorter_than_32_bytes(string environment)
    {
        AssertStartupFails(environment, signingKey: null);
        AssertStartupFails(environment, signingKey: new string('k', 31));

        using var host = BuildHost(environment, new string('k', 32), out var logs);
        host.Start();

        var options = host.Services.GetRequiredService<IOptions<GuardianConsentOptions>>().Value;
        Assert.Equal(new string('k', 32), options.SigningKey);
        Assert.DoesNotContain(
            logs.Entries,
            entry => entry.Level == LogLevel.Warning && entry.Message == GuardianConsentOptions.DevelopmentSigningKeyWarning);
    }

    private static void AssertStartupFails(string environment, string? signingKey)
    {
        var exception = Record.Exception(() =>
        {
            using var host = BuildHost(environment, signingKey, out _);
            host.Start();
        });

        Assert.NotNull(exception);
        Assert.Contains(GuardianConsentOptions.MissingSigningKeyMessage, exception.ToString());
    }

    private static IHost BuildHost(string environment, string? signingKey, out CollectingLoggerProvider logs)
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
        };
        if (signingKey is not null)
        {
            values["GuardianConsent:SigningKey"] = signingKey;
        }

        builder.Configuration.AddInMemoryCollection(values);
        logs = new CollectingLoggerProvider();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Services.AddSituConfiguration(builder.Configuration);
        return builder.Build();
    }

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CollectingLogger(List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
