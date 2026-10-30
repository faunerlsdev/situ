using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Legal;

namespace Situ.Api.Administration;

public static class TermsV11Command
{
    public static Task<int> ActivateAsync(IServiceProvider services) => RunAsync(services, activate: true);

    public static Task<int> RevertAsync(IServiceProvider services) => RunAsync(services, activate: false);

    private static async Task<int> RunAsync(IServiceProvider services, bool activate)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var result = activate
            ? await TermsV11Switch.ActivateAsync(db, CancellationToken.None)
            : await TermsV11Switch.RevertAsync(db, CancellationToken.None);
        if (result.IsFailure)
        {
            Console.Error.WriteLine(result.Error!.Message);
            return 1;
        }

        Console.WriteLine(activate
            ? "Términos 1.1 activos. La 2026-10-29 quedó inactiva."
            : "Términos 2026-10-29 activos. La 1.1 quedó inactiva.");
        return 0;
    }
}
