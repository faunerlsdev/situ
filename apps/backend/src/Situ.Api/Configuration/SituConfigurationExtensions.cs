using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Situ.Infrastructure.Guardianship;

namespace Situ.Api.Configuration;

public static class SituConfigurationExtensions
{
    public const string MissingJwtSecretMessage =
        "Falta Jwt:Secret. En desarrollo configúralo con dotnet user-secrets. En producción define la variable de entorno Jwt__Secret.";

    public const string ShortJwtSecretMessage =
        "Jwt:Secret debe tener al menos 32 caracteres.";

    public const string MissingConnectionStringMessage =
        "Falta la cadena de conexión ConnectionStrings:Default. En desarrollo configúrala con dotnet user-secrets. En producción define la variable de entorno ConnectionStrings__Default.";

    public static IServiceCollection AddSituConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Secret), MissingJwtSecretMessage)
            .Validate(
                static options => string.IsNullOrWhiteSpace(options.Secret) || options.Secret.Length >= 32,
                ShortJwtSecretMessage)
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Default), MissingConnectionStringMessage)
            .ValidateOnStart();

        services.AddOptions<GuardianConsentOptions>()
            .Bind(configuration.GetSection(GuardianConsentOptions.SectionName))
            .PostConfigure<IHostEnvironment, ILogger<GuardianConsentOptions>>((options, environment, logger) =>
            {
                options.SigningKey = (options.SigningKey ?? "").Trim();
                if (!environment.IsDevelopment() || options.SigningKey.Length > 0)
                {
                    return;
                }

                options.SigningKey = GuardianConsentOptions.DevelopmentSigningKey;
                logger.LogWarning(GuardianConsentOptions.DevelopmentSigningKeyWarning);
            })
            .Validate<IHostEnvironment>(
                static (options, environment) =>
                    environment.IsDevelopment()
                    || (!string.IsNullOrWhiteSpace(options.SigningKey)
                        && Encoding.UTF8.GetByteCount(options.SigningKey) >= 32),
                GuardianConsentOptions.MissingSigningKeyMessage)
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ProductionGate>, ProductionGateValidator>();
        services.AddOptions<ProductionGate>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<NetworkOptions>, NetworkOptionsValidator>();
        services.AddOptions<NetworkOptions>()
            .Bind(configuration.GetSection(NetworkOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<NetworkOptions>>((forwarded, network) => ForwardedClient.Apply(forwarded, network.Value.KnownProxies));

        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<GuardianConsentOptions>>().Value);
        services.AddSingleton(PublicBaseUrlOptions.From(configuration));

        return services;
    }
}
