using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Situ.Application.Features.Administration.Staff;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence;

namespace Situ.Application.Tests.Administration;

public class AdminPanelPostgresTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [PostgresFact]
    public async Task Grant_persists_on_situ_test_and_rolls_back()
    {
        var connectionString = TestConnection.Resolve()!;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        await using var db = new AppDbContext(options);
        if (!await db.Database.CanConnectAsync())
        {
            return;
        }

        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var city = await db.Cities.FirstAsync();
        var user = User.Register(
            Email.Create($"admin-pg-{Guid.NewGuid():N}@situ.test"),
            UserName.Create("admpg" + Guid.NewGuid().ToString("N")[..6]),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+573007001122"),
            "hash",
            new DateOnly(1992, 1, 1),
            city.CountryId,
            city.RegionId,
            city.Id,
            "es-CO",
            14,
            Now);
        user.ConfirmEmail(Now);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var granted = await new GrantStaffRoleHandler(db, new FixedClock(Now)).Handle(
            new GrantStaffRoleRequest(null, user.Email.Value, "SuperAdmin", null),
            CancellationToken.None);
        Assert.Equal("Super admin", granted.Value!.RoleName);
        Assert.True(await db.StaffAssignments.AnyAsync(item => item.Id == granted.Value.AssignmentId && item.IsActive));
        await transaction.RollbackAsync();
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            var configured = TestConnection.Resolve();
            if (string.IsNullOrWhiteSpace(configured))
            {
                Skip = "ConnectionStrings__Test no está configurada. Se omite la prueba del panel contra situ_test.";
                return;
            }

            try
            {
                var database = new NpgsqlConnectionStringBuilder(configured).Database;
                if (!string.Equals(database, "situ_test", StringComparison.OrdinalIgnoreCase))
                {
                    Skip = "ConnectionStrings__Test no apunta a situ_test. Se omite la prueba del panel.";
                }
            }
            catch (ArgumentException)
            {
                Skip = "ConnectionStrings__Test no se pudo leer. Se omite la prueba del panel.";
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

            if (document.RootElement.TryGetProperty("ConnectionStrings", out var nested)
                && nested.TryGetProperty("Test", out var test))
            {
                return test.GetString();
            }

            return null;
        }
    }
}
