using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity.RefreshSession;
using Situ.Application.Features.Identity.SignIn;
using Situ.Application.Features.Identity.SignOut;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Tests.Identity;

public class SignInHandlerTests
{
    [Fact]
    public async Task Four_wrong_passwords_stay_unauthorized_and_the_fifth_locks_for_fifteen_minutes()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedUserAsync(db);
        var handler = new SignInHandler(db, clock, new FakePasswords(), new FakeTokens());

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await handler.Handle(Request("ana@situ.test", "Mala1234", "phone"), CancellationToken.None);
            Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
            Assert.Equal("auth.invalid_credentials", result.Error.Code);
            Assert.Equal("Correo, usuario o contraseña incorrectos.", result.Error.Message);
        }

        var locked = await handler.Handle(Request("ana@situ.test", "Mala1234", "phone"), CancellationToken.None);

        Assert.Equal(ErrorType.Locked, locked.Error!.Type);
        Assert.Equal("auth.locked", locked.Error.Code);
        Assert.Equal(RegistrationCatalog.Now.AddMinutes(15), locked.Error.Extensions["retryAfter"]);
        Assert.Equal(5, (await db.Users.SingleAsync()).FailedLoginAttempts);
        Assert.Equal(user.LockoutEndAt, RegistrationCatalog.Now.AddMinutes(15));
        Assert.Equal(5, await db.LoginAttempts.CountAsync(attempt => attempt.UserId == user.Id));
    }

    [Fact]
    public async Task A_sixth_attempt_during_the_window_does_not_extend_the_lockout()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await SeedUserAsync(db);
        var handler = new SignInHandler(db, clock, new FakePasswords(), new FakeTokens());

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await handler.Handle(Request("ana@situ.test", "Mala1234", "phone"), CancellationToken.None);
        }

        var again = await handler.Handle(Request("ana@situ.test", "Mala1234", "phone"), CancellationToken.None);
        var user = await db.Users.SingleAsync();

        Assert.Equal(ErrorType.Locked, again.Error!.Type);
        Assert.Equal(user.LockoutEndAt, again.Error.Extensions["retryAfter"]);
        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.Equal(6, await db.LoginAttempts.CountAsync());
    }

    [Fact]
    public async Task The_correct_password_works_again_after_the_lockout_window()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await SeedUserAsync(db);
        var handler = new SignInHandler(db, clock, new FakePasswords(), new FakeTokens());

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await handler.Handle(Request("ana@situ.test", "Mala1234", "phone"), CancellationToken.None);
        }

        clock.UtcNow = RegistrationCatalog.Now.AddMinutes(15);
        var result = await handler.Handle(Request("ana@situ.test", "Clave123", "phone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = await db.Users.SingleAsync();
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndAt);
        Assert.Equal(clock.UtcNow, user.LastLoginAt);
    }

    [Fact]
    public async Task Unknown_login_and_wrong_password_share_the_same_response()
    {
        await using var db = IdentityTestDb.Create();
        var handler = new SignInHandler(db, new FixedClock(RegistrationCatalog.Now), new FakePasswords(), new FakeTokens());
        await SeedUserAsync(db);

        var unknown = await handler.Handle(Request("nadie@situ.test", "Clave123", "phone"), CancellationToken.None);
        var wrong = await handler.Handle(Request("ana@situ.test", "Mala1234", "phone"), CancellationToken.None);

        Assert.Equal(unknown.Error!.Code, wrong.Error!.Code);
        Assert.Equal(unknown.Error.Message, wrong.Error.Message);
        Assert.Equal("Correo, usuario o contraseña incorrectos.", unknown.Error.Message);
        Assert.Null((await db.LoginAttempts.SingleAsync(attempt => attempt.EmailAttempted == "nadie@situ.test")).UserId);
    }

    [Fact]
    public async Task A_successful_sign_in_records_the_ip_and_issues_a_rotating_refresh_token()
    {
        await using var db = IdentityTestDb.Create();
        var user = await SeedUserAsync(db);
        var tokens = new FakeTokens();
        var handler = new SignInHandler(db, new FixedClock(RegistrationCatalog.Now), new FakePasswords(), tokens);

        var result = await handler.Handle(
            Request("ANA", "Clave123", "phone") with { IpAddress = "203.0.113.8", UserAgent = "SituTests" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("access:" + user.Id.ToString("N"), result.Value!.AccessToken);
        Assert.Equal("hash:" + result.Value.RefreshToken, (await db.RefreshTokens.SingleAsync()).TokenHash);

        var attempt = await db.LoginAttempts.SingleAsync();
        Assert.True(attempt.Succeeded);
        Assert.Equal(user.Id, attempt.UserId);
        Assert.Equal("203.0.113.8", attempt.IpAddress);
        Assert.Equal("ANA", attempt.EmailAttempted);
    }

    [Fact]
    public async Task The_third_device_is_rejected_and_an_existing_device_can_sign_in_again()
    {
        await using var db = IdentityTestDb.Create();
        var handler = new SignInHandler(db, new FixedClock(RegistrationCatalog.Now), new FakePasswords(), new FakeTokens());
        await SeedUserAsync(db);

        Assert.True((await handler.Handle(Request("ana@situ.test", "Clave123", "device-1"), CancellationToken.None)).IsSuccess);
        Assert.True((await handler.Handle(Request("ana@situ.test", "Clave123", "device-2"), CancellationToken.None)).IsSuccess);

        var third = await handler.Handle(Request("ana@situ.test", "Clave123", "device-3"), CancellationToken.None);
        Assert.Equal("auth.device_limit", third.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, third.Error.Type);
        Assert.Equal("device_management", third.Error.Extensions["scope"]);
        Assert.Contains("device_management", third.Error.Extensions["accessToken"]!.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, await db.UserDevices.CountAsync());

        var again = await handler.Handle(Request("ana@situ.test", "Clave123", "device-1"), CancellationToken.None);
        Assert.True(again.IsSuccess);
        Assert.Equal(2, await db.UserDevices.CountAsync(device => device.RevokedAt == null));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reuse_revokes_that_device_family()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var tokens = new FakeTokens();
        await SeedUserAsync(db);
        var signIn = new SignInHandler(db, clock, new FakePasswords(), tokens);
        var refresh = new RefreshSessionHandler(db, clock, tokens);

        var first = await signIn.Handle(Request("ana@situ.test", "Clave123", "phone"), CancellationToken.None);
        var second = await signIn.Handle(Request("ana@situ.test", "Clave123", "tablet"), CancellationToken.None);
        var rotated = await refresh.Handle(new RefreshSessionRequest(first.Value!.RefreshToken, "203.0.113.9"), CancellationToken.None);

        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(first.Value.RefreshToken, rotated.Value!.RefreshToken);
        Assert.Equal(2, await db.RefreshTokens.CountAsync(token => token.UserDeviceId == first.Value.DeviceId));

        var reused = await refresh.Handle(new RefreshSessionRequest(first.Value.RefreshToken, "203.0.113.9"), CancellationToken.None);
        Assert.Equal("auth.session_invalid", reused.Error!.Code);

        var stolen = await refresh.Handle(new RefreshSessionRequest(rotated.Value.RefreshToken, "203.0.113.9"), CancellationToken.None);
        Assert.Equal("auth.session_invalid", stolen.Error!.Code);
        Assert.NotNull((await db.UserDevices.SingleAsync(device => device.Id == first.Value.DeviceId)).RevokedAt);
        Assert.Equal(0, await db.RefreshTokens.CountAsync(token => token.UserDeviceId == first.Value.DeviceId && token.RevokedAt == null));

        var other = await refresh.Handle(new RefreshSessionRequest(second.Value!.RefreshToken, "203.0.113.10"), CancellationToken.None);
        Assert.True(other.IsSuccess);
        Assert.Null((await db.UserDevices.SingleAsync(device => device.Id == second.Value.DeviceId)).RevokedAt);
    }

    [Fact]
    public async Task Sign_out_makes_the_refresh_token_unusable()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var tokens = new FakeTokens();
        await SeedUserAsync(db);
        var signedIn = await new SignInHandler(db, clock, new FakePasswords(), tokens)
            .Handle(Request("ana@situ.test", "Clave123", "phone"), CancellationToken.None);

        var signedOut = await new SignOutHandler(db, clock, tokens)
            .Handle(new SignOutRequest(signedIn.Value!.RefreshToken), CancellationToken.None);
        var again = await new SignOutHandler(db, clock, tokens)
            .Handle(new SignOutRequest("missing-token"), CancellationToken.None);
        var refreshed = await new RefreshSessionHandler(db, clock, tokens)
            .Handle(new RefreshSessionRequest(signedIn.Value.RefreshToken, null), CancellationToken.None);

        Assert.True(signedOut.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.Equal("auth.session_invalid", refreshed.Error!.Code);
        Assert.NotNull((await db.UserDevices.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task Unconfirmed_email_rejects_sign_in_without_counting_as_a_bad_password()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = User.Register(
            Email.Create("ana@situ.test"),
            UserName.Create("ana"),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+573001234567"),
            "hashed:Clave123",
            DateOnly.FromDateTime(RegistrationCatalog.Now.UtcDateTime).AddYears(-30),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es-CO",
            14,
            RegistrationCatalog.Now);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var handler = new SignInHandler(db, clock, new FakePasswords(), new FakeTokens());

        var result = await handler.Handle(Request("ana@situ.test", "Clave123", "phone"), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Equal("auth.email_unconfirmed", result.Error.Code);
        Assert.Equal("Tu correo todavía no está confirmado.", result.Error.Message);
        Assert.Equal(0, (await db.Users.SingleAsync()).FailedLoginAttempts);
    }

    [Fact]
    public async Task Pending_guardian_consent_returns_a_restricted_token_instead_of_a_session()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = User.Register(
            Email.Create("menor@situ.test"),
            UserName.Create("menor"),
            PersonName.Create("Luz"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+573001234567"),
            "hashed:Clave123",
            DateOnly.FromDateTime(RegistrationCatalog.Now.UtcDateTime).AddYears(-15),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es-CO",
            14,
            RegistrationCatalog.Now);
        user.ConfirmEmail(RegistrationCatalog.Now);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await new SignInHandler(db, clock, new FakePasswords(), new FakeTokens())
            .Handle(Request("menor@situ.test", "Clave123", "phone"), CancellationToken.None);

        Assert.Equal("auth.guardian_consent_required", result.Error!.Code);
        Assert.Equal("guardian_consent", result.Error.Extensions["scope"]);
        Assert.Contains("guardian_consent", result.Error.Extensions["accessToken"]!.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, (await db.Users.SingleAsync()).FailedLoginAttempts);
        Assert.Empty(db.UserDevices);
    }

    private static SignInRequest Request(string login, string password, string device) =>
        new(login, password, device, DevicePlatform.Web, "Tests", "1.0", null, "127.0.0.1", "tests");

    private static async Task<User> SeedUserAsync(IdentityTestDb db)
    {
        var user = User.Register(
            Email.Create("ana@situ.test"),
            UserName.Create("ana"),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+573001234567"),
            "hashed:Clave123",
            DateOnly.FromDateTime(RegistrationCatalog.Now.UtcDateTime).AddYears(-30),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es-CO",
            14,
            RegistrationCatalog.Now);
        user.ConfirmEmail(RegistrationCatalog.Now);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class FakeTokens : ITokenService
    {
        public string CreateAccessToken(Guid userId, DateTimeOffset expiresAt, string? scope = null, string? role = null, Guid? deviceId = null) =>
            "access:" + userId.ToString("N") + ":" + expiresAt.UtcTicks + ":" + (scope ?? "session");

        public string CreateRefreshToken() => Guid.NewGuid().ToString("N");

        public string Hash(string plainToken) => "hash:" + plainToken;
    }
}
