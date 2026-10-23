using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Situ.Application.Common.Abstractions;
using Situ.Infrastructure.Guardianship;
using Situ.Infrastructure.Mail;
using Situ.Infrastructure.Identity;
using Situ.Infrastructure.Persistence;
using Situ.Infrastructure.Persistence.Interceptors;

namespace Situ.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
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
        return services;
    }
}
