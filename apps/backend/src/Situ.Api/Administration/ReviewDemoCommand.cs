using Situ.Application.Common.Abstractions;
using Situ.Application.Development;

namespace Situ.Api.Administration;

public static class ReviewDemoCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services)
    {
        var reviewPassword = Environment.GetEnvironmentVariable("SITU_REVIEW_DEMO_PASSWORD");
        var deletePassword = Environment.GetEnvironmentVariable("SITU_REVIEW_DELETE_PASSWORD");
        if (string.IsNullOrWhiteSpace(reviewPassword) || string.IsNullOrWhiteSpace(deletePassword))
        {
            Console.Error.WriteLine("Define SITU_REVIEW_DEMO_PASSWORD y SITU_REVIEW_DELETE_PASSWORD. No las escribas en el comando.");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var result = await ReviewDemoSeed.ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            scope.ServiceProvider.GetRequiredService<IFileStorage>(),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            reviewPassword,
            deletePassword,
            CancellationToken.None);
        if (result.IsFailure)
        {
            Console.Error.WriteLine(result.Error!.Message);
            return 1;
        }

        Console.WriteLine("Cuentas demo listas. Las contraseñas no se imprimen.");
        Console.WriteLine($"Revisión: {ReviewDemoSeed.ReviewEmailAddress} ({ReviewDemoSeed.ReviewUserName})");
        Console.WriteLine($"Eliminar cuenta: {ReviewDemoSeed.DeleteEmailAddress} ({ReviewDemoSeed.DeleteUserName})");
        return 0;
    }
}
