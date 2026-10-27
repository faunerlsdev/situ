using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Reports.GetReportMap;
using Situ.Infrastructure.Guardianship;
using Situ.Infrastructure.Mail;
using Situ.Infrastructure.Identity;
using Situ.Infrastructure.Persistence;
using Situ.Infrastructure.Persistence.Interceptors;
using Situ.Infrastructure.Reports;

namespace Situ.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string? environmentName = null)
    {
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<DomainEventSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Falta la cadena de conexión ConnectionStrings:Default. En desarrollo configúrala con dotnet user-secrets. En producción define la variable de entorno ConnectionStrings__Default.");
            }

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventSaveChangesInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());

        var email = EmailOptions.From(configuration);
        services.AddSingleton(email);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IPublicLinks, PublicLinks>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        var captchaProvider = configuration["Captcha:Provider"];
        if (string.IsNullOrWhiteSpace(captchaProvider))
        {
            captchaProvider = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ? "Development" : "Turnstile";
        }

        services.AddSingleton(new Situ.Application.Features.Configuration.GetAppConfig.CaptchaPublicView(
            captchaProvider,
            configuration["Captcha:SiteKey"] ?? string.Empty));
        if (string.Equals(captchaProvider, "Development", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICaptchaVerifier, Captcha.DevelopmentCaptchaVerifier>();
        }
        else
        {
            services.AddSingleton<ICaptchaVerifier>(serviceProvider =>
                new Captcha.TurnstileCaptchaVerifier(new HttpClient(), serviceProvider.GetRequiredService<IConfiguration>()));
        }

        services.AddSingleton<IGuardianConsentSigner, HmacGuardianConsentSigner>();

        var storageRoot = configuration["FileStorage:Root"];
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            storageRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "situ",
                "uploads");
        }

        var signingKey = configuration["FileStorage:SigningKey"];
        var development = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            if (!development)
            {
                throw new InvalidOperationException(FileStorageOptions.MissingSigningKeyMessage);
            }

            signingKey = FileStorageOptions.DevelopmentSigningKey;
        }
        else if (!development && string.Equals(signingKey, FileStorageOptions.DevelopmentSigningKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(FileStorageOptions.MissingSigningKeyMessage);
        }

        var storageOptions = new FileStorageOptions { Root = storageRoot, SigningKey = signingKey };
        services.AddSingleton(storageOptions);
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IMediaUrlSigner, HmacMediaUrlSigner>();
        services.AddSingleton<IReportMediaProcessor, SkiaReportMediaProcessor>();
        services.AddSingleton<IAvatarImageProcessor, SkiaAvatarProcessor>();
        services.AddSingleton<IAdminIconProcessor, SkiaAdminIconProcessor>();
        services.AddScoped<IReportMapReader, ReportMapReader>();
        services.AddScoped<IAlertLocationQuery, Notifications.PostGisAlertLocationQuery>();
        services.AddSingleton<IPushSender>(serviceProvider =>
        {
            var environment = environmentName
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";
            var require = Notifications.ExpoPushPolicy.RequireAccessToken(environment, ReadOptionalBool(configuration["Notifications:RequireExpoAccessToken"]));
            var expoToken = configuration["Notifications:ExpoAccessToken"];
            var delivery = Notifications.ExpoPushPolicy.Decide(require, expoToken);
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Situ.Notifications");
            Notifications.ExpoPushStartup.WarnIfDisabled(logger, delivery);
            if (delivery == Notifications.ExpoPushDelivery.Disabled)
            {
                return new Notifications.DisabledPushSender();
            }

            return new Notifications.ExpoPushSender(new HttpClient(), logger, delivery == Notifications.ExpoPushDelivery.Authorized ? expoToken : null);
        });

        return services;
    }

    private static bool? ReadOptionalBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return bool.TryParse(raw, out var value) ? value : true;
    }
}
