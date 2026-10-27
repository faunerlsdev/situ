using Situ.Application.Common.Abstractions;

namespace Situ.Api.Administration;

public static class MailTestCommand
{
    public const string Subject = "SITU prueba de correo";

    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        var to = Value(args, "--to");
        if (string.IsNullOrWhiteSpace(to) || !to.Contains('@', StringComparison.Ordinal) || to.StartsWith('-'))
        {
            Console.Error.WriteLine("Uso: dotnet run --project apps/backend/src/Situ.Api -- mail:test --to <correo>");
            return 1;
        }

        var mail = services.GetRequiredService<IEmailSender>();
        try
        {
            await mail.SendAsync(to, Subject, "Este es un mensaje de prueba de SITU.", CancellationToken.None);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"No se pudo enviar el correo de prueba ({exception.GetType().Name}).");
            return 1;
        }

        Console.WriteLine("Correo de prueba enviado.");
        return 0;
    }

    private static string? Value(string[] args, string name)
    {
        var index = Array.FindIndex(args, argument => string.Equals(argument, name, StringComparison.Ordinal));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
