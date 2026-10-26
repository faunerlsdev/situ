namespace Situ.Api.Administration;

public static class ConsoleCommands
{
    public static bool IsRequested(string[] args) =>
        args.Length > 0 && args[0] is "admin:grant" or "admin:reset-2fa" or "dev:seed";

    public static Task<int> ExecuteAsync(WebApplication app, string[] args) => args[0] switch
    {
        "admin:grant" => AdminGrant.ExecuteAsync(app.Services, args),
        "admin:reset-2fa" => AdminResetTwoFactor.ExecuteAsync(app.Services, args),
        "dev:seed" => DevSeedCommand.ExecuteAsync(app.Services, app.Environment, args),
        _ => Task.FromResult(1),
    };
}
