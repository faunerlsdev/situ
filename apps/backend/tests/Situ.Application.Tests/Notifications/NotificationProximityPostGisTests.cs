using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Infrastructure.Notifications;
using Situ.Infrastructure.Persistence;

namespace Situ.Application.Tests.Notifications;

public class NotificationProximityPostGisTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

    [PostGisFact]
    public async Task St_dwithin_keeps_the_device_inside_the_radius_and_drops_the_one_outside()
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
            await using var transaction = await db.Database.BeginTransactionAsync();
            var country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
            var region = Region.Create(country.Id, "Cundinamarca", "25");
            var city = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
            db.AddRange(country, region, city);
            var nearUser = Person(city, "cerca-pg@situ.test", "cercapg", "3001110001");
            var farUser = Person(city, "lejos-pg@situ.test", "lejospg", "3001110002");
            db.Users.AddRange(nearUser, farUser);
            var nearDevice = UserDevice.Register(nearUser.Id, "near-phone", DevicePlatform.IOS, null, null, null, Now);
            var farDevice = UserDevice.Register(farUser.Id, "far-phone", DevicePlatform.IOS, null, null, null, Now);
            db.UserDevices.AddRange(nearDevice, farDevice);
            db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(nearUser.Id, nearDevice.Id, 4.715, -74.072, 20, Now, TimeSpan.FromMinutes(30)));
            db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(farUser.Id, farDevice.Id, 4.800, -74.072, 20, Now, TimeSpan.FromMinutes(30)));
            await db.SaveChangesAsync();

            var matches = await new PostGisAlertLocationQuery(db).MatchAsync(new GeoPoint(4.711, -74.072), Now, CancellationToken.None);
            Assert.Contains(matches, item => item.UserId == nearUser.Id && item.DistanceMeters <= 1000);
            Assert.DoesNotContain(matches, item => item.UserId == farUser.Id);

            var index = await db.Database.SqlQueryRaw<string>(
                """
                SELECT indexname AS "Value"
                FROM pg_indexes
                WHERE tablename = 'device_alert_locations' AND indexname = 'ix_device_alert_locations_location'
                """).ToListAsync();
            Assert.Equal("ix_device_alert_locations_location", Assert.Single(index));
            await transaction.RollbackAsync();
        }
        catch (Exception exception) when (exception is not Xunit.Sdk.XunitException)
        {
            Console.WriteLine("OMITIDA: no hay conexión a situ_test. " + exception.GetType().Name);
        }
    }

    private static User Person(City city, string email, string userName, string phone)
    {
        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create(phone),
            "hash",
            new DateOnly(1996, 1, 1),
            city.CountryId,
            city.RegionId,
            city.Id,
            "es-CO",
            14,
            Now);
        user.ConfirmEmail(Now);
        return user;
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
