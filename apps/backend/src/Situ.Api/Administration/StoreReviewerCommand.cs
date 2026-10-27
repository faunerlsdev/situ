using Situ.Application.Common.Abstractions;
using Situ.Application.Development;

namespace Situ.Api.Administration;

public static class StoreReviewerCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await StoreReviewerSeed.ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            CancellationToken.None);
        if (result.IsFailure)
        {
            Console.Error.WriteLine(result.Error!.Message);
            return 1;
        }

        Console.WriteLine("Cuenta de revisión lista.");
        Console.WriteLine($"Correo: {StoreReviewerSeed.EmailAddress}");
        Console.WriteLine($"Usuario: {StoreReviewerSeed.UserNameValue}");
        Console.WriteLine($"Contraseña: {result.Value}");
        Console.WriteLine("No se guardó la contraseña. Cópiala ahora en App Store Connect y Play Console.");
        return 0;
    }
}
