using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Administration.Staff;

namespace Situ.Api.Administration;

public static class AdminGrant
{
    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "admin:grant", StringComparison.Ordinal);

    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var email = Value(args, "--email");
        var role = Value(args, "--role");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
        {
            Console.Error.WriteLine("Uso: dotnet run --project apps/backend/src/Situ.Api -- admin:grant --email <correo> --role SuperAdmin");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IHandler<GrantStaffRoleRequest, Result<GrantStaffRoleResponse>>>();
        var result = await handler.Handle(new GrantStaffRoleRequest(null, email, role, null), CancellationToken.None);
        if (result.IsFailure)
        {
            Console.Error.WriteLine(result.Error!.Message);
            return 1;
        }

        Console.WriteLine("Rol asignado.");
        return 0;
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, argument => string.Equals(argument, name, StringComparison.Ordinal));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
