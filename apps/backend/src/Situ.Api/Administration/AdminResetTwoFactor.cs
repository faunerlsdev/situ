using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Administration.Auth;

namespace Situ.Api.Administration;

public static class AdminResetTwoFactor
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var email = Value(args, "--email");
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("Uso: dotnet run --project apps/backend/src/Situ.Api -- admin:reset-2fa --email <correo>");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var result = await StaffTwoFactorConsole.ResetAsync(db, clock, email, CancellationToken.None);
        if (result.IsFailure)
        {
            Console.Error.WriteLine(result.Error!.Message);
            return 1;
        }

        Console.WriteLine("Segundo factor restablecido. La cuenta debe inscribirse de nuevo.");
        return 0;
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, argument => string.Equals(argument, name, StringComparison.Ordinal));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
