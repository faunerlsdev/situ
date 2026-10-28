namespace Situ.Api.Administration;

public static class ConsoleCommands
{
    public static bool IsRequested(string[] args) =>
        args.Length > 0 && args[0] is "admin:grant" or "admin:reset-2fa" or "categories:import-icons" or "dev:seed" or "migrate" or "mail:test" or "store:seed-reviewer";

    public static Task<int> ExecuteAsync(WebApplication app, string[] args) => args[0] switch
    {
        "admin:grant" => AdminGrant.ExecuteAsync(app.Services, args),
        "admin:reset-2fa" => AdminResetTwoFactor.ExecuteAsync(app.Services, args),
        "categories:import-icons" => ImportCategoryIconsCommand.ExecuteAsync(app, args),
        "dev:seed" => DevSeedCommand.ExecuteAsync(app.Services, app.Environment, args),
        "migrate" => MigrateCommand.ExecuteAsync(app.Services),
        "mail:test" => MailTestCommand.ExecuteAsync(app.Services, args),
        "store:seed-reviewer" => StoreReviewerCommand.ExecuteAsync(app.Services),
        _ => Task.FromResult(1),
    };
}
