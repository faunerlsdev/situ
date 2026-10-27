using System.Text;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Administration.Auth;
using Situ.Application.Features.Identity.RefreshSession;
using Situ.Application.Features.Identity.SignIn;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;

namespace Situ.Application.Tests.Administration;

public class StaffMfaFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Staff_sign_in_and_refresh_are_rejected_on_the_public_login()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var staff = Person(db, "equipo@correo.com", "equipo");
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.UsersView);
        var tokens = new Tokens();
        var signIn = new SignInHandler(db, clock, new FakePasswords(), tokens);

        var login = await signIn.Handle(new SignInRequest("equipo@correo.com", "Clave123", "phone", DevicePlatform.IOS, null, null, null, "127.0.0.1", "tests"), CancellationToken.None);

        Assert.Equal("auth.staff_use_panel", login.Error!.Code);
        Assert.Equal("Las cuentas del equipo ingresan por el panel de administración.", login.Error.Message);
        var device = UserDevice.Register(staff.Id, "phone", DevicePlatform.IOS, null, null, null, Now);
        db.UserDevices.Add(device);
        db.RefreshTokens.Add(RefreshToken.Issue(staff.Id, device.Id, tokens.Hash("refresh-1"), Now, SessionPolicy.RefreshTokenLifetime));
        await db.SaveChangesAsync();
        var refresh = await new RefreshSessionHandler(db, clock, tokens).Handle(new RefreshSessionRequest("refresh-1", "127.0.0.1"), CancellationToken.None);

        Assert.Equal("auth.staff_use_panel", refresh.Error!.Code);
        Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task Panel_login_enrolls_verifies_and_rejects_a_reused_code()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var staff = Person(db, "mod@correo.com", "modera");
        var outsider = Person(db, "ana@correo.com", "ana");
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.UsersView);
        var tokens = new Tokens();
        var protector = new Protector();
        var passwords = new FakePasswords();
        var login = new AdminLoginHandler(db, clock, passwords, tokens);

        var unknown = await login.Handle(new AdminLoginRequest("nadie@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var normal = await login.Handle(new AdminLoginRequest("ana@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var enrolled = await login.Handle(new AdminLoginRequest("mod@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);

        Assert.Equal(unknown.Error!.Code, normal.Error!.Code);
        Assert.Equal("Correo o contraseña incorrectos.", normal.Error.Message);
        Assert.True(enrolled.Value!.MfaEnrollmentRequired);
        Assert.StartsWith("mfa_enrollment|", enrolled.Value.EnrollmentToken);
        Assert.DoesNotContain("|Staff|", enrolled.Value.EnrollmentToken);

        var setup = await new SetupStaffTwoFactorHandler(db, clock, protector).Handle(new SetupStaffTwoFactorRequest(staff.Id, "127.0.0.1"), CancellationToken.None);
        var stored = (await db.Users.SingleAsync(user => user.Id == staff.Id)).TwoFactorSecret!;
        Assert.StartsWith("enc:", stored);
        Assert.DoesNotContain(setup.Value!.Secret, stored);
        Assert.DoesNotContain(setup.Value.Secret, (await db.AuditLogs.SingleAsync(item => item.Action == "staff.mfa_setup")).Changes);

        var code = Totp.Code(setup.Value.Secret, Now);
        var enabled = await new EnableStaffTwoFactorHandler(db, clock, protector, passwords, tokens)
            .Handle(new EnableStaffTwoFactorRequest(staff.Id, code, "127.0.0.1", "tests"), CancellationToken.None);

        Assert.Equal(10, enabled.Value!.RecoveryCodes.Count);
        Assert.StartsWith("mfa|Staff|", enabled.Value.AccessToken);
        Assert.DoesNotContain(setup.Value.Secret, (await db.AuditLogs.SingleAsync(item => item.Action == "staff.mfa_enabled")).Changes!);
        Assert.True((await db.Users.SingleAsync(user => user.Id == staff.Id)).TwoFactorEnabled);

        var again = await login.Handle(new AdminLoginRequest("mod@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var reused = await new VerifyStaffMfaHandler(db, clock, protector, passwords, tokens)
            .Handle(new VerifyStaffMfaRequest(again.Value!.MfaToken!, code, null, "127.0.0.1", "tests"), CancellationToken.None);

        Assert.Equal("mfa.code_reused", reused.Error!.Code);
    }

    [Fact]
    public async Task A_recovery_code_works_once_and_five_failures_lock_the_factor()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var staff = Person(db, "soporte@correo.com", "soporte");
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.UsersView);
        var tokens = new Tokens();
        var protector = new Protector();
        var passwords = new FakePasswords();
        await new AdminLoginHandler(db, clock, passwords, tokens).Handle(new AdminLoginRequest("soporte@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var setup = await new SetupStaffTwoFactorHandler(db, clock, protector).Handle(new SetupStaffTwoFactorRequest(staff.Id, "127.0.0.1"), CancellationToken.None);
        var enabled = await new EnableStaffTwoFactorHandler(db, clock, protector, passwords, tokens)
            .Handle(new EnableStaffTwoFactorRequest(staff.Id, Totp.Code(setup.Value!.Secret, Now), "127.0.0.1", "tests"), CancellationToken.None);
        var verify = new VerifyStaffMfaHandler(db, clock, protector, passwords, tokens);
        var login = new AdminLoginHandler(db, clock, passwords, tokens);
        var firstChallenge = await login.Handle(new AdminLoginRequest("soporte@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var used = await verify.Handle(new VerifyStaffMfaRequest(firstChallenge.Value!.MfaToken!, null, enabled.Value!.RecoveryCodes[0], "127.0.0.1", "tests"), CancellationToken.None);
        var secondChallenge = await login.Handle(new AdminLoginRequest("soporte@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var replay = await verify.Handle(new VerifyStaffMfaRequest(secondChallenge.Value!.MfaToken!, null, enabled.Value.RecoveryCodes[0], "127.0.0.1", "tests"), CancellationToken.None);

        Assert.True(used.IsSuccess);
        Assert.Equal("mfa.recovery_invalid", replay.Error!.Code);
        Assert.DoesNotContain(enabled.Value.RecoveryCodes[0], (await db.AuditLogs.OrderBy(item => item.OccurredAt).LastAsync(item => item.Action.StartsWith("staff.mfa_"))).Changes ?? "");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var challenge = await login.Handle(new AdminLoginRequest("soporte@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
            var failed = await verify.Handle(new VerifyStaffMfaRequest(challenge.Value!.MfaToken!, "000000", null, "127.0.0.1", "tests"), CancellationToken.None);
            Assert.Equal("mfa.code_invalid", failed.Error!.Code);
        }

        var lastChallenge = await login.Handle(new AdminLoginRequest("soporte@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var locked = await verify.Handle(new VerifyStaffMfaRequest(lastChallenge.Value!.MfaToken!, "000000", null, "127.0.0.1", "tests"), CancellationToken.None);

        Assert.Equal("mfa.locked", locked.Error!.Code);
        Assert.NotNull((await db.Users.SingleAsync(user => user.Id == staff.Id)).MfaLockoutEndAt);
    }

    [Fact]
    public async Task Reset_clears_another_account_and_refuses_the_actor()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var admin = Person(db, "root@correo.com", "root");
        var other = Person(db, "otro@correo.com", "otro");
        await db.SaveChangesAsync();
        await Grant(db, admin.Id, StaffPermissionCodes.StaffManage, "Super admin");
        await Grant(db, other.Id, StaffPermissionCodes.UsersView);
        var tokens = new Tokens();
        var protector = new Protector();
        var passwords = new FakePasswords();
        await new AdminLoginHandler(db, clock, passwords, tokens).Handle(new AdminLoginRequest("otro@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        var setup = await new SetupStaffTwoFactorHandler(db, clock, protector).Handle(new SetupStaffTwoFactorRequest(other.Id, "127.0.0.1"), CancellationToken.None);
        await new EnableStaffTwoFactorHandler(db, clock, protector, passwords, tokens)
            .Handle(new EnableStaffTwoFactorRequest(other.Id, Totp.Code(setup.Value!.Secret, Now), "127.0.0.1", "tests"), CancellationToken.None);
        var reset = new ResetStaffTwoFactorHandler(db, clock);

        var self = await reset.Handle(new ResetStaffTwoFactorRequest(admin.Id, "127.0.0.1", admin.Id, "Perdí el teléfono"), CancellationToken.None);
        var otherReset = await reset.Handle(new ResetStaffTwoFactorRequest(admin.Id, "127.0.0.1", other.Id, "Perdió el teléfono"), CancellationToken.None);
        var changes = (await db.AuditLogs.SingleAsync(item => item.Action == "staff.mfa_reset")).Changes!;

        Assert.Equal("mfa.reset_self", self.Error!.Code);
        Assert.True(otherReset.IsSuccess);
        Assert.False((await db.Users.SingleAsync(user => user.Id == other.Id)).TwoFactorEnabled);
        Assert.Contains("reason", changes);
        Assert.DoesNotContain(setup.Value.Secret, changes);
        var next = await new AdminLoginHandler(db, clock, passwords, tokens).Handle(new AdminLoginRequest("otro@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        Assert.True(next.Value!.MfaEnrollmentRequired);
    }

    [Fact]
    public async Task Five_unknown_panel_logins_are_rate_limited()
    {
        await using var db = IdentityTestDb.Create();
        var login = new AdminLoginHandler(db, new FixedClock(Now), new FakePasswords(), new Tokens());
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var result = await login.Handle(new AdminLoginRequest("nadie@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
            Assert.Equal("auth.invalid_credentials", result.Error!.Code);
        }

        var limited = await login.Handle(new AdminLoginRequest("nadie@correo.com", "Clave123", "127.0.0.1", "tests"), CancellationToken.None);
        Assert.Equal("auth.rate_limited", limited.Error!.Code);
    }

    private static User Person(IdentityTestDb db, string email, string userName)
    {
        var country = db.Countries.Local.FirstOrDefault();
        var region = db.Regions.Local.FirstOrDefault();
        var city = db.Cities.Local.FirstOrDefault();
        if (country is null || region is null || city is null)
        {
            country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
            region = Region.Create(country.Id, "Cundinamarca", "25");
            city = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
            db.AddRange(country, region, city);
        }

        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+57300555" + (2000 + db.Users.Local.Count).ToString()),
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

    private static async Task Grant(IdentityTestDb db, Guid userId, string permission, string? roleName = null)
    {
        var role = StaffRole.Create(roleName ?? permission, permission, false, Now);
        db.StaffRoles.Add(role);
        db.StaffPermissions.Add(StaffPermission.Grant(role.Id, permission));
        db.StaffAssignments.Add(StaffAssignment.Assign(userId, role.Id, null, Now));
        await db.SaveChangesAsync();
    }

    private sealed class Protector : ISecretProtector
    {
        public string Protect(string plaintext) => "enc:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));

        public string? Unprotect(string protectedText) =>
            protectedText.StartsWith("enc:", StringComparison.Ordinal)
                ? Encoding.UTF8.GetString(Convert.FromBase64String(protectedText[4..]))
                : null;
    }

    private sealed class Tokens : ITokenService
    {
        public string CreateAccessToken(Guid userId, DateTimeOffset expiresAt, string? scope = null, string? role = null, Guid? deviceId = null) =>
            (scope ?? "") + "|" + (role ?? "") + "|" + userId.ToString("N") + "|" + expiresAt.UtcTicks.ToString();

        public string CreateRefreshToken() => Guid.NewGuid().ToString("N");

        public string Hash(string plainToken) => "hash:" + plainToken;

        public AccessTokenTicket? ReadAccessToken(string token)
        {
            var parts = token.Split('|');
            if (parts.Length != 4 || !Guid.TryParse(parts[2], out var userId) || !long.TryParse(parts[3], out var ticks))
            {
                return null;
            }

            return new AccessTokenTicket(
                userId,
                string.IsNullOrEmpty(parts[0]) ? null : parts[0],
                string.IsNullOrEmpty(parts[1]) ? null : parts[1],
                new DateTimeOffset(ticks, TimeSpan.Zero));
        }
    }
}
