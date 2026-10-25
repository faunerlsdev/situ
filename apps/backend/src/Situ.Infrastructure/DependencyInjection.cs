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
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            signingKey = "situ-development-media-signing-key";
        }

        var storageOptions = new FileStorageOptions { Root = storageRoot, SigningKey = signingKey };
        services.AddSingleton(storageOptions);
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IMediaUrlSigner, HmacMediaUrlSigner>();
        services.AddSingleton<IReportMediaProcessor, SkiaReportMediaProcessor>();
        services.AddSingleton<IAvatarImageProcessor, SkiaAvatarProcessor>();
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
