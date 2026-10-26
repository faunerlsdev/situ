using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Administration.Auth;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;

namespace Situ.Application.Tests.Administration;

public class StaffTwoFactorConsoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private const string Secret = "enc:super-secret-value";

    [Fact]
    public async Task Reset_clears_the_authenticator_revokes_sessions_and_audits_without_the_email_or_secret()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var staff = Person(db, "equipo@correo.com", "equipo");
        staff.EnableTwoFactor(Secret);
        var device = UserDevice.Register(staff.Id, "phone", DevicePlatform.IOS, null, null, null, Now);
        db.UserDevices.Add(device);
        var active = RefreshToken.Issue(staff.Id, device.Id, "hash:active", Now, TimeSpan.FromDays(1));
        var already = RefreshToken.Issue(staff.Id, device.Id, "hash:old", Now.AddMinutes(-5), TimeSpan.FromDays(1));
        already.Revoke(Now.AddMinutes(-1));
        db.RefreshTokens.AddRange(active, already);
        db.TwoFactorRecoveryCodes.Add(TwoFactorRecoveryCode.Issue(staff.Id, "hash:code", Now));
        await db.SaveChangesAsync();
        await Grant(db, staff.Id);

        var result = await StaffTwoFactorConsole.ResetAsync(db, clock, "equipo@correo.com", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await db.Users.SingleAsync(user => user.Id == staff.Id);
        Assert.Null(stored.TwoFactorSecret);
        Assert.False(stored.TwoFactorEnabled);
        Assert.Empty(await db.TwoFactorRecoveryCodes.ToListAsync());
        Assert.NotNull((await db.RefreshTokens.SingleAsync(token => token.TokenHash == "hash:active")).RevokedAt);
        Assert.True((await db.UserDevices.SingleAsync()).IsActive);
        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal(StaffTwoFactorConsole.Action, audit.Action);
        Assert.Null(audit.ActorUserId);
        Assert.Null(audit.IpAddress);
        Assert.Equal(staff.Id.ToString(), audit.EntityId);
        Assert.DoesNotContain("equipo@correo.com", audit.Changes);
        Assert.DoesNotContain(Secret, audit.Changes);

        var login = await new AdminLoginHandler(db, clock, new FakePasswords(), new Tokens())
            .Handle(new AdminLoginRequest("equipo@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        Assert.True(login.Value!.MfaEnrollmentRequired);
    }

    [Fact]
    public async Task Reset_fails_when_the_account_is_missing_or_is_not_staff_and_does_not_store_the_email()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var person = Person(db, "ana@correo.com", "ana");
        person.EnableTwoFactor(Secret);
        await db.SaveChangesAsync();

        var invalid = await StaffTwoFactorConsole.ResetAsync(db, clock, "no es correo", CancellationToken.None);
        var missing = await StaffTwoFactorConsole.ResetAsync(db, clock, "nadie@correo.com", CancellationToken.None);
        var outsider = await StaffTwoFactorConsole.ResetAsync(db, clock, "ana@correo.com", CancellationToken.None);

        Assert.Equal("staff.invalid_email", invalid.Error!.Code);
        Assert.Equal("staff.user_not_found", missing.Error!.Code);
        Assert.Equal("staff.not_staff", outsider.Error!.Code);
        Assert.Equal(Secret, (await db.Users.SingleAsync()).TwoFactorSecret);
        var audits = await db.AuditLogs.OrderBy(item => item.OccurredAt).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.Equal(StaffTwoFactorConsole.Action, audit.Action));
        Assert.Contains(audits, audit => audit.Changes!.Contains("user_not_found", StringComparison.Ordinal) && audit.EntityId is null);
        Assert.Contains(audits, audit => audit.Changes!.Contains("not_staff", StringComparison.Ordinal));
        Assert.DoesNotContain(audits, audit => (audit.Changes ?? "").Contains("nadie@correo.com", StringComparison.Ordinal));
        Assert.DoesNotContain(audits, audit => (audit.Changes ?? "").Contains("ana@correo.com", StringComparison.Ordinal));
        Assert.DoesNotContain(audits, audit => (audit.Changes ?? "").Contains(Secret, StringComparison.Ordinal));
    }

    private static User Person(IdentityTestDb db, string email, string userName)
    {
        var country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
        var region = Region.Create(country.Id, "Cundinamarca", "25");
        var city = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
        db.AddRange(country, region, city);
        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("3005550199"),
            "hashed:Clave123",
            new DateOnly(1990, 1, 1),
            country.Id,
            region.Id,
            city.Id,
            "es-CO",
            14,
            Now);
        user.ConfirmEmail(Now);
        db.Users.Add(user);
        return user;
    }

    private static async Task Grant(IdentityTestDb db, Guid userId)
    {
        var role = StaffRole.Create("Moderador", "Modera", false, Now);
        db.StaffRoles.Add(role);
        db.StaffPermissions.Add(StaffPermission.Grant(role.Id, StaffPermissionCodes.UsersView));
        db.StaffAssignments.Add(StaffAssignment.Assign(userId, role.Id, null, Now));
        await db.SaveChangesAsync();
    }

    private sealed class Tokens : ITokenService
    {
        public string CreateAccessToken(Guid userId, DateTimeOffset expiresAt, string? scope = null, string? role = null, Guid? deviceId = null) => "access";

        public string CreateRefreshToken() => "refresh";

        public string Hash(string plainToken) => "hash:" + plainToken;

        public AccessTokenTicket? ReadAccessToken(string token) => null;
    }
}
