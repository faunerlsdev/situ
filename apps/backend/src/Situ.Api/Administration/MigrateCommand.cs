using Microsoft.EntityFrameworkCore;
using Situ.Infrastructure.Persistence;

namespace Situ.Api.Administration;

public static class MigrateCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'situ_app') THEN
                        GRANT USAGE ON SCHEMA public TO situ_app;
                        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO situ_app;
                        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO situ_app;
                        GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO situ_app;
                    END IF;
                END $$;
                """);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"No se pudieron aplicar las migraciones ({exception.GetType().Name}).");
            return 1;
        }

        Console.WriteLine("Migraciones aplicadas.");
        return 0;
    }
}
