using Situ.Application.Common.Abstractions;
using Situ.Application.Development;

namespace Situ.Api.Administration;

public static class DevSeedCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, IHostEnvironment environment, string[] args)
    {
        var clean = args.Any(argument => string.Equals(argument, "--clean", StringComparison.Ordinal));
        await using var scope = services.CreateAsyncScope();
        var result = await DevSeed.ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            environment.IsDevelopment(),
            clean,
            CancellationToken.None);
        if (result.IsFailure)
        {
            Console.Error.WriteLine(result.Error!.Message);
            return 1;
        }

        Console.WriteLine(clean
            ? "Datos de ejemplo borrados."
            : "Datos de ejemplo listos. Las cuentas y la contraseña están en docs/admin.md.");
        return 0;
    }
}
