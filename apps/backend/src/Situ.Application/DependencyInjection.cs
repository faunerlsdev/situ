using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Situ.Application.Behaviors;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, TimeProviderClock>();
        RegisterHandlers(services);
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        var handlerDefinition = typeof(IHandler<,>);
        var registered = new HashSet<Type>();

        var implementations = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var implementation in implementations)
        {
            foreach (var service in implementation.GetInterfaces())
            {
                if (!service.IsGenericType || service.GetGenericTypeDefinition() != handlerDefinition)
                {
                    continue;
                }

                if (registered.Add(implementation))
                {
                    services.AddScoped(implementation);
                }

                var arguments = service.GetGenericArguments();
                var responseType = arguments[1];
                if (!typeof(Result).IsAssignableFrom(responseType))
                {
                    services.AddScoped(service, implementation);
                    continue;
                }

                var decoratorType = typeof(ValidationHandlerDecorator<,>).MakeGenericType(arguments);
                services.AddScoped(service, provider =>
                {
                    var inner = provider.GetRequiredService(implementation);
                    return ActivatorUtilities.CreateInstance(provider, decoratorType, inner);
                });
            }
        }
    }
}
