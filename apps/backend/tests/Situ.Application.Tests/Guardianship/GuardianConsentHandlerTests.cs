using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Guardianship.AuthorizeGuardianConsent;
using Situ.Application.Features.Guardianship.DeclineGuardianConsent;
using Situ.Application.Features.Guardianship.ExpireGuardianConsents;
using Situ.Application.Features.Guardianship.SubmitGuardianData;
using Situ.Application.Features.Guardianship.VerifyGuardianEmail;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;

namespace Situ.Application.Tests.Guardianship;

public class GuardianConsentHandlerTests
{
    [Fact]
    public async Task Authorizing_a_verified_guardian_activates_the_minor_and_sends_the_receipt()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedMinorAsync(db, clock, confirmEmail: true);
        await SeedConsentAsync(db, user.Id, "public-token", clock.UtcNow);

        var submitted = await Submit(db, email, clock).Handle(Data("public-token"), CancellationToken.None);
        var verified = await Verify(db, clock).Handle(
            new VerifyGuardianEmailRequest(RegistrationCatalog.TokenFrom(email.Messages[0].Body)),
            CancellationToken.None);
        var authorized = await Authorize(db, email, clock).Handle(
            new AuthorizeGuardianConsentRequest("public-token", false, true, "203.0.113.8", "SituTests"),
            CancellationToken.None);

        Assert.True(submitted.IsSuccess);
        Assert.Equal("carlos@correo.com", email.Messages[0].To);
        Assert.True(verified.IsSuccess);
        Assert.True(authorized.IsSuccess);
        Assert.Equal(UserStatus.Active, authorized.Value!.Status);

        var storedUser = await db.Users.SingleAsync();
        Assert.Equal(UserStatus.Active, storedUser.Status);
        Assert.Equal(clock.UtcNow, storedUser.GuardianConsentGrantedAt);

        var consent = await db.GuardianConsents.SingleAsync();
        Assert.Equal(GuardianConsentStatus.Authorized, consent.Status);
        Assert.False(consent.LiveStreamingAuthorized);
        Assert.True(consent.LocationSharingAuthorized);
        Assert.Equal("203.0.113.8", consent.AuthorizationIpAddress);
        Assert.StartsWith("sig:", consent.DigitalSignature);
        Assert.Equal(clock.UtcNow, consent.ReceiptSentAt);
        Assert.Contains(consent.DigitalSignature!, email.Messages[1].Body);
        Assert.Equal("Constancia de autorización en SITU", email.Messages[1].Subject);
        Assert.Equal("carlos@correo.com", email.Messages[1].To);
        Assert.Contains("Compartir ubicación con amigos: sí", email.Messages[1].Body);
        Assert.Contains("Transmisión en vivo: no", email.Messages[1].Body);
    }

    [Fact]
    public async Task Authorization_without_a_confirmed_minor_email_waits_for_that_confirmation()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedMinorAsync(db, clock, confirmEmail: false);
        await SeedConsentAsync(db, user.Id, "public-token", clock.UtcNow);
        await Submit(db, email, clock).Handle(Data("public-token"), CancellationToken.None);
        await Verify(db, clock).Handle(
            new VerifyGuardianEmailRequest(RegistrationCatalog.TokenFrom(email.Messages[0].Body)),
            CancellationToken.None);

        var authorized = await Authorize(db, email, clock).Handle(
            new AuthorizeGuardianConsentRequest("public-token", false, false, null, null),
            CancellationToken.None);

        Assert.Equal(UserStatus.PendingEmailConfirmation, authorized.Value!.Status);
        Assert.NotNull((await db.Users.SingleAsync()).GuardianConsentGrantedAt);
    }

    [Fact]
    public async Task Authorization_requires_the_verified_email_and_a_known_link()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedMinorAsync(db, clock, confirmEmail: true);
        await SeedConsentAsync(db, user.Id, "public-token", clock.UtcNow);

        var unknown = await Authorize(db, email, clock).Handle(
            new AuthorizeGuardianConsentRequest("otro-token", true, true, null, null),
            CancellationToken.None);
        var tooSoon = await Authorize(db, email, clock).Handle(
            new AuthorizeGuardianConsentRequest("public-token", true, true, null, null),
            CancellationToken.None);

        Assert.Equal("guardian_consent.not_found", unknown.Error!.Code);
        Assert.Equal("guardian_consent.email_not_verified", tooSoon.Error!.Code);
        Assert.Equal(UserStatus.PendingGuardianConsent, (await db.Users.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_new_form_replaces_the_previous_verification_link()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedMinorAsync(db, clock, confirmEmail: true);
        await SeedConsentAsync(db, user.Id, "public-token", clock.UtcNow);
        var submit = Submit(db, email, clock);
        await submit.Handle(Data("public-token"), CancellationToken.None);
        var first = RegistrationCatalog.TokenFrom(email.Messages[0].Body);

        await submit.Handle(Data("public-token") with { ContactEmail = "otro@correo.com" }, CancellationToken.None);
        var reused = await Verify(db, clock).Handle(new VerifyGuardianEmailRequest(first), CancellationToken.None);
        var current = await Verify(db, clock).Handle(
            new VerifyGuardianEmailRequest(RegistrationCatalog.TokenFrom(email.Messages[1].Body)),
            CancellationToken.None);

        Assert.Equal("security_token.already_used", reused.Error!.Code);
        Assert.True(current.IsSuccess);
        Assert.Equal("otro@correo.com", (await db.GuardianConsents.SingleAsync()).GuardianContactEmail);
    }

    [Fact]
    public async Task Declining_leaves_the_minor_waiting_for_a_guardian()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedMinorAsync(db, clock, confirmEmail: true);
        await SeedConsentAsync(db, user.Id, "public-token", clock.UtcNow);

        var declined = await new DeclineGuardianConsentHandler(db, clock)
            .Handle(new DeclineGuardianConsentRequest("public-token"), CancellationToken.None);
        var again = await Authorize(db, new FakeEmailSender(), clock).Handle(
            new AuthorizeGuardianConsentRequest("public-token", true, true, null, null),
            CancellationToken.None);

        Assert.True(declined.IsSuccess);
        Assert.Equal("guardian_consent.not_open", again.Error!.Code);
        var stored = await db.Users.SingleAsync();
        Assert.Equal(UserStatus.PendingGuardianConsent, stored.Status);
        Assert.Null(stored.GuardianConsentGrantedAt);
        Assert.Equal(GuardianConsentStatus.Declined, (await db.GuardianConsents.SingleAsync()).Status);
    }

    [Fact]
    public async Task The_job_expires_unanswered_requests_after_seven_days_and_keeps_an_authorization()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await SeedMinorAsync(db, clock, confirmEmail: true);
        var keep = GuardianConsent.Request(user.Id, "tutor@situ.test", Hash("keep"), "2026-01", clock.UtcNow, clock.UtcNow);
        keep.SubmitGuardianData("Carlos Pérez", GuardianDocumentType.Passport, "P123", "carlos@correo.com", clock.UtcNow);
        keep.VerifyGuardianEmail(clock.UtcNow);
        keep.Authorize("signature", true, false, null, null, clock.UtcNow);
        var drop = GuardianConsent.Request(user.Id, "otro@situ.test", Hash("drop"), "2026-01", clock.UtcNow, clock.UtcNow);
        db.AddRange(keep, drop);
        await db.SaveChangesAsync();
        var job = new ExpireGuardianConsentsHandler(db, clock);

        var early = await job.Handle(new ExpireGuardianConsentsRequest(), CancellationToken.None);
        clock.UtcNow = clock.UtcNow.Add(MinorProtectionPolicy.DefaultGuardianResponseWindow);
        var due = await job.Handle(new ExpireGuardianConsentsRequest(), CancellationToken.None);
        var repeated = await job.Handle(new ExpireGuardianConsentsRequest(), CancellationToken.None);
        var submit = await Submit(db, new FakeEmailSender(), clock).Handle(Data("drop"), CancellationToken.None);

        Assert.Equal(0, early.Value!.Expired);
        Assert.Equal(1, due.Value!.Expired);
        Assert.Equal(0, repeated.Value!.Expired);
        Assert.Equal(GuardianConsentStatus.Authorized, (await db.GuardianConsents.SingleAsync(item => item.Id == keep.Id)).Status);
        Assert.Equal(GuardianConsentStatus.Expired, (await db.GuardianConsents.SingleAsync(item => item.Id == drop.Id)).Status);
        Assert.Equal("guardian_consent.not_open", submit.Error!.Code);
    }

    private static SubmitGuardianDataRequest Data(string token) =>
        new(token, "Carlos Pérez", GuardianDocumentType.CitizenshipCard, "1234567890", "carlos@correo.com");

    private static SubmitGuardianDataHandler Submit(IdentityTestDb db, FakeEmailSender email, FixedClock clock) =>
        new(db, clock, email, new FakeLinks());

    private static VerifyGuardianEmailHandler Verify(IdentityTestDb db, FixedClock clock) => new(db, clock);

    private static AuthorizeGuardianConsentHandler Authorize(IdentityTestDb db, FakeEmailSender email, FixedClock clock) =>
        new(db, clock, new FakeSigner(), email);

    private static async Task<User> SeedMinorAsync(IdentityTestDb db, FixedClock clock, bool confirmEmail)
    {
        var user = User.Register(
            Email.Create("menor@situ.test"),
            UserName.Create("menor"),
            "hashed:Clave123",
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(-14),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es-CO",
            14,
            clock.UtcNow);
        if (confirmEmail)
        {
            user.ConfirmEmail(clock.UtcNow);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedConsentAsync(IdentityTestDb db, Guid minorId, string token, DateTimeOffset now)
    {
        db.GuardianConsents.Add(GuardianConsent.Request(
            minorId,
            "tutor@situ.test",
            Hash(token),
            "2026-01",
            now,
            now));
        await db.SaveChangesAsync();
    }

    private static string Hash(string plain) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));

    private sealed class FakeSigner : IGuardianConsentSigner
    {
        public string Sign(GuardianConsentSignature input) => "sig:" + input.ConsentId.ToString("N");
    }
}
