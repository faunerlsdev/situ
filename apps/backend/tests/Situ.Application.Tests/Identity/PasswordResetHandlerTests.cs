using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity.ChangePassword;
using Situ.Application.Features.Identity.ForgotPassword;
using Situ.Application.Features.Identity.RefreshSession;
using Situ.Application.Features.Identity.ResetPassword;
using Situ.Application.Features.Identity.SignIn;
using Situ.Application.Features.Identity.ValidateResetToken;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Tests.Identity;

public class ForgotPasswordHandlerTests
{
    [Fact]
    public async Task Unknown_and_invalid_addresses_succeed_without_sending_mail()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        await SeedUserAsync(db);
        var handler = Forgot(db, email, new FixedClock(RegistrationCatalog.Now));

        var unknown = await handler.Handle(new ForgotPasswordRequest("nadie@situ.test"), CancellationToken.None);
        var invalid = await handler.Handle(new ForgotPasswordRequest("no-es-un-correo"), CancellationToken.None);

        Assert.True(unknown.IsSuccess);
        Assert.True(invalid.IsSuccess);
        Assert.Empty(email.Messages);
        Assert.Empty(db.SecurityTokens);
    }

    [Fact]
    public async Task A_known_address_receives_one_single_use_link_that_expires_in_thirty_minutes()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedUserAsync(db);

        var sent = await Forgot(db, email, clock).Handle(new ForgotPasswordRequest("ANA@situ.test"), CancellationToken.None);
        var repeated = await Forgot(db, email, clock).Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);

        Assert.True(sent.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Single(email.Messages);
        Assert.Equal("ana@situ.test", email.Messages[0].To);
        Assert.Equal("Restablece tu contraseña en SITU", email.Messages[0].Subject);
        Assert.Contains("token=", email.Messages[0].Body);

        var stored = await db.SecurityTokens.SingleAsync();
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(SecurityTokenPurpose.PasswordReset, stored.Purpose);
        Assert.Equal(clock.UtcNow.Add(PasswordResetPolicy.TokenLifetime), stored.ExpiresAt);
        Assert.Null(stored.ConsumedAt);
        Assert.True((await Validate(db, clock).Handle(new ValidateResetTokenRequest(Token(email)), CancellationToken.None)).IsSuccess);
        Assert.Null((await db.SecurityTokens.SingleAsync()).ConsumedAt);
    }

    [Fact]
    public async Task A_later_request_replaces_the_previous_link()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await SeedUserAsync(db);
        var handler = Forgot(db, email, clock);
        await handler.Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);
        var first = Token(email);

        clock.UtcNow = clock.UtcNow.Add(PasswordResetPolicy.MinimumRequestInterval);
        var sent = await handler.Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);

        Assert.True(sent.IsSuccess);
        Assert.Equal(2, email.Messages.Count);
        Assert.Equal("security_token.already_used", (await Validate(db, clock).Handle(new ValidateResetTokenRequest(first), CancellationToken.None)).Error!.Code);
        Assert.True((await Validate(db, clock).Handle(new ValidateResetTokenRequest(Token(email)), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task A_failed_delivery_still_succeeds_and_can_be_requested_again()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender { Failure = new InvalidOperationException("smtp") };
        var clock = new FixedClock(RegistrationCatalog.Now);
        await SeedUserAsync(db);
        var handler = Forgot(db, email, clock);

        var failed = await handler.Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);

        Assert.True(failed.IsSuccess);
        Assert.Empty(email.Messages);
        Assert.DoesNotContain(await db.SecurityTokens.ToListAsync(), token => token.IsValid(clock.UtcNow));

        email.Failure = null;
        var sent = await handler.Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);
        Assert.True(sent.IsSuccess);
        Assert.Single(email.Messages);
    }

    private static ForgotPasswordHandler Forgot(IdentityTestDb db, FakeEmailSender email, FixedClock clock) =>
        new(db, clock, email, new FakeLinks());

    private static ValidateResetTokenHandler Validate(IdentityTestDb db, FixedClock clock) => new(db, clock);

    private static string Token(FakeEmailSender email) => RegistrationCatalog.TokenFrom(email.Messages[^1].Body);

    internal static async Task<User> SeedUserAsync(IdentityTestDb db)
    {
        var user = User.Register(
            Email.Create("ana@situ.test"),
            UserName.Create("ana"),
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
        return user;
    }
}

public class ResetPasswordHandlerTests
{
    [Fact]
    public async Task Reset_unlocks_the_account_invalidates_tokens_and_sends_a_security_notice()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var tokens = new FakeTokens();
        var user = await ForgotPasswordHandlerTests.SeedUserAsync(db);
        var confirmation = SecurityToken.Issue(
            user.Id,
            SecurityTokenPurpose.EmailConfirmation,
            "confirm-hash",
            clock.UtcNow,
            EmailConfirmationPolicy.TokenLifetime);
        var device = UserDevice.Register(user.Id, "phone", DevicePlatform.IOS, null, null, null, clock.UtcNow);
        var refresh = RefreshToken.Issue(user.Id, device.Id, tokens.Hash("refresh-1"), clock.UtcNow, SessionPolicy.RefreshTokenLifetime);
        db.AddRange(confirmation, device, refresh);
        await db.SaveChangesAsync();

        var signIn = new SignInHandler(db, clock, new FakePasswords(), tokens);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await signIn.Handle(Login("Mala1234"), CancellationToken.None);
        }

        Assert.True((await db.Users.SingleAsync()).IsLockedOut(clock.UtcNow));
        await new ForgotPasswordHandler(db, clock, email, new FakeLinks())
            .Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);
        var plain = RegistrationCatalog.TokenFrom(email.Messages[0].Body);

        var weak = await Reset(db, email, clock).Handle(new ResetPasswordRequest(plain, "abcdefgh1"), CancellationToken.None);
        Assert.Equal("password.invalid", weak.Error!.Code);
        Assert.True((await Validate(db, clock).Handle(new ValidateResetTokenRequest(plain), CancellationToken.None)).IsSuccess);

        var reset = await Reset(db, email, clock).Handle(new ResetPasswordRequest(plain, "Nueva123"), CancellationToken.None);
        var again = await Reset(db, email, clock).Handle(new ResetPasswordRequest(plain, "Nueva123"), CancellationToken.None);

        Assert.True(reset.IsSuccess);
        Assert.Equal("security_token.already_used", again.Error!.Code);
        var stored = await db.Users.SingleAsync();
        Assert.Equal("hashed:Nueva123", stored.PasswordHash);
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.LockoutEndAt);
        Assert.Equal("Tu contraseña de SITU cambió", email.Messages[^1].Subject);
        Assert.Equal("ana@situ.test", email.Messages[^1].To);
        Assert.DoesNotContain(await db.SecurityTokens.ToListAsync(), token => token.IsValid(clock.UtcNow));
        Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAt);

        var refreshed = await new RefreshSessionHandler(db, clock, tokens)
            .Handle(new RefreshSessionRequest("refresh-1", null), CancellationToken.None);
        Assert.Equal("auth.session_invalid", refreshed.Error!.Code);
        Assert.True((await signIn.Handle(Login("Nueva123"), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task An_expired_link_does_not_change_the_password()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await ForgotPasswordHandlerTests.SeedUserAsync(db);
        await new ForgotPasswordHandler(db, clock, email, new FakeLinks())
            .Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);
        clock.UtcNow = clock.UtcNow.Add(PasswordResetPolicy.TokenLifetime);

        var result = await Reset(db, email, clock).Handle(
            new ResetPasswordRequest(RegistrationCatalog.TokenFrom(email.Messages[0].Body), "Nueva123"),
            CancellationToken.None);

        Assert.Equal("security_token.expired", result.Error!.Code);
        Assert.Equal("hashed:Clave123", (await db.Users.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task The_password_stays_changed_when_the_security_notice_fails()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await ForgotPasswordHandlerTests.SeedUserAsync(db);
        await new ForgotPasswordHandler(db, clock, email, new FakeLinks())
            .Handle(new ForgotPasswordRequest("ana@situ.test"), CancellationToken.None);
        email.Failure = new InvalidOperationException("smtp");

        var result = await Reset(db, email, clock).Handle(
            new ResetPasswordRequest(RegistrationCatalog.TokenFrom(email.Messages[0].Body), "Nueva123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hashed:Nueva123", (await db.Users.SingleAsync()).PasswordHash);
        Assert.NotNull((await db.SecurityTokens.SingleAsync(token => token.Purpose == SecurityTokenPurpose.PasswordReset)).ConsumedAt);
    }

    private static ResetPasswordHandler Reset(IdentityTestDb db, FakeEmailSender email, FixedClock clock) =>
        new(db, clock, new FakePasswords(), email);

    private static ValidateResetTokenHandler Validate(IdentityTestDb db, FixedClock clock) => new(db, clock);

    private static SignInRequest Login(string password) =>
        new("ana@situ.test", password, "phone", DevicePlatform.Web, null, null, null, "127.0.0.1", "tests");

    private sealed class FakeTokens : ITokenService
    {
        public string CreateAccessToken(Guid userId, DateTimeOffset expiresAt) => "access";

        public string CreateRefreshToken() => Guid.NewGuid().ToString("N");

        public string Hash(string plainToken) => "hash:" + plainToken;
    }
}

public class ChangePasswordHandlerTests
{
    [Fact]
    public async Task Requires_the_signed_in_user_and_the_current_password()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await ForgotPasswordHandlerTests.SeedUserAsync(db);

        var anonymous = await Change(db, clock, null).Handle(new ChangePasswordRequest("Clave123", "Nueva123"), CancellationToken.None);
        var wrong = await Change(db, clock, user.Id).Handle(new ChangePasswordRequest("Mala1234", "Nueva123"), CancellationToken.None);
        var weak = await Change(db, clock, user.Id).Handle(new ChangePasswordRequest("Clave123", "abcdefgh1"), CancellationToken.None);

        Assert.Equal("auth.unauthenticated", anonymous.Error!.Code);
        Assert.Equal("password.current_invalid", wrong.Error!.Code);
        Assert.Equal("password.invalid", weak.Error!.Code);
        Assert.Equal("hashed:Clave123", (await db.Users.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task Changes_the_password_without_clearing_a_lockout()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await ForgotPasswordHandlerTests.SeedUserAsync(db);
        for (var attempt = 0; attempt < LockoutPolicy.Default.MaxFailedAttempts; attempt++)
        {
            user.RegisterFailedLogin(clock.UtcNow, LockoutPolicy.Default);
        }

        await db.SaveChangesAsync();

        var result = await Change(db, clock, user.Id).Handle(new ChangePasswordRequest("Clave123", "Nueva123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await db.Users.SingleAsync();
        Assert.Equal("hashed:Nueva123", stored.PasswordHash);
        Assert.True(stored.IsLockedOut(clock.UtcNow));
        Assert.Equal(clock.UtcNow, stored.LastPasswordChangeAt);
    }

    private static ChangePasswordHandler Change(IdentityTestDb db, FixedClock clock, Guid? userId) =>
        new(db, clock, new FakePasswords(), new FakeUser(userId));

    private sealed class FakeUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;

        public bool IsAuthenticated => UserId is not null;
    }
}
