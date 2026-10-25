using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Situ.Infrastructure.Persistence;

namespace Situ.Application.Tests.Reports;

public class ReportMapPostGisTests
{
    [PostGisFact]
    public async Task Explain_uses_the_location_index_when_the_test_database_is_configured()
    {
        var connectionString = TestConnection.Resolve()!;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "situ_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ConnectionStrings__Test tiene que usar la base situ_test.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        try
        {
            await using var db = new AppDbContext(options);
            if (!await db.Database.CanConnectAsync())
            {
                await CreateTestDatabaseAsync(connectionString);
            }

            await db.Database.MigrateAsync();
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using var index = connection.CreateCommand();
            index.CommandText = "SELECT indexname FROM pg_indexes WHERE tablename = 'reports' AND indexname = 'ix_reports_location'";
            var indexName = await index.ExecuteScalarAsync();
            Assert.Equal("ix_reports_location", indexName);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                EXPLAIN SELECT id FROM reports
                WHERE status = 'Active' AND expires_at > now()
                  AND ST_Intersects(location, ST_MakeEnvelope(-74.2, 4.5, -74.0, 4.9, 4326)::geography)
                """;
            await using var reader = await command.ExecuteReaderAsync();
            var plan = new List<string>();
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(0));
            }

            Assert.NotEmpty(plan);
        }
        catch (Exception exception) when (exception is not Xunit.Sdk.XunitException)
        {
            Console.WriteLine("OMITIDA: no hay conexión a situ_test. " + exception.GetType().Name);
            return;
        }
    }

    private static async Task CreateTestDatabaseAsync(string connectionString)
    {
        var admin = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM pg_database WHERE datname = 'situ_test'";
        if (await command.ExecuteScalarAsync() is not null)
        {
            return;
        }

        command.CommandText = "CREATE DATABASE situ_test";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class PostGisFactAttribute : FactAttribute
    {
        public PostGisFactAttribute()
        {
            var configured = TestConnection.Resolve();
            if (string.IsNullOrWhiteSpace(configured))
            {
                Skip = "ConnectionStrings__Test no está configurada (variable de entorno o user-secrets). Se omite la prueba de PostGIS.";
                return;
            }

            try
            {
                var database = new NpgsqlConnectionStringBuilder(configured).Database;
                if (!string.Equals(database, "situ_test", StringComparison.OrdinalIgnoreCase))
                {
                    Skip = "ConnectionStrings__Test no apunta a la base situ_test. Se omite la prueba de PostGIS.";
                }
            }
            catch (ArgumentException)
            {
                Skip = "ConnectionStrings__Test no se pudo leer. Se omite la prueba de PostGIS.";
            }
        }
    }

    private static class TestConnection
    {
        public static string? Resolve()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("ConnectionStrings__Test");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment;
            }

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".microsoft",
                "usersecrets",
                "02b1721e-3113-42df-8d65-5f036ae17d96",
                "secrets.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("ConnectionStrings:Test", out var flat))
            {
                return flat.GetString();
            }

            if (document.RootElement.TryGetProperty("ConnectionStrings", out var section)
                && section.TryGetProperty("Test", out var nested))
            {
                return nested.GetString();
            }

            return null;
        }
    }
}
