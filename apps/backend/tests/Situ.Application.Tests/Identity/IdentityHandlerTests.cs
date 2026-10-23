using Microsoft.EntityFrameworkCore;
using Situ.Application.Features.Identity.ConfirmEmail;
using Situ.Application.Features.Identity.RegisterUser;
using Situ.Application.Features.Identity.ResendConfirmationEmail;
using Situ.Application.Tests.Support;
using Situ.Domain.Identity;
using Situ.Domain.Legal;

namespace Situ.Application.Tests.Identity;

public class RegisterUserHandlerTests
{
    [Fact]
    public async Task Adult_is_pending_email_confirmation_and_accepts_current_documents()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var email = new FakeEmailSender();
        var handler = Handler(db, email);

        var result = await handler.Handle(RegistrationCatalog.Request(catalog, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.PendingEmailConfirmation, result.Value!.Status);

        var user = await db.Users.SingleAsync();
        Assert.Equal("hashed:Clave123", user.PasswordHash);
        Assert.Equal(catalog.CityId, user.CityId);
        Assert.Equal("es-CO", user.Locale);
        Assert.Null(user.EmailConfirmedAt);

        var consents = await db.ConsentRecords.ToListAsync();
        Assert.Equal(2, consents.Count);
        Assert.All(consents, consent =>
        {
            Assert.Equal(user.Id, consent.UserId);
            Assert.Equal(ConsentMethod.Registration, consent.Method);
            Assert.Equal("2026-01", consent.Version);
            Assert.Equal("127.0.0.1", consent.IpAddress);
        });
        Assert.Contains(consents, consent => consent.DocumentType == LegalDocumentType.TermsOfUse);
        Assert.Contains(consents, consent => consent.DocumentType == LegalDocumentType.PrivacyPolicy);

        Assert.Single(email.Messages);
        Assert.Equal("ana@situ.test", email.Messages[0].To);
        Assert.Equal("Confirma tu correo en SITU", email.Messages[0].Subject);
        Assert.Contains("Hola ana", email.Messages[0].Body);
        Assert.Contains("token=", email.Messages[0].Body);
    }

    [Fact]
    public async Task Minor_at_the_country_minimum_age_waits_for_a_guardian()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var handler = Handler(db, new FakeEmailSender());

        var result = await handler.Handle(RegistrationCatalog.Request(catalog, 14, email: "menor@situ.test"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.PendingGuardianConsent, result.Value!.Status);
        Assert.Null((await db.Users.SingleAsync()).GuardianConsentGrantedAt);
    }

    [Fact]
    public async Task Rejects_an_age_below_the_country_minimum_without_saving()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var email = new FakeEmailSender();
        var handler = Handler(db, email);

        var result = await handler.Handle(RegistrationCatalog.Request(catalog, 13), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("user.below_minimum_age", result.Error!.Code);
        Assert.Empty(db.Users);
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Rejects_a_password_outside_the_policy()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var handler = Handler(db, new FakeEmailSender());

        var result = await handler.Handle(RegistrationCatalog.Request(catalog, 20, password: "abcdefgh1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("password.invalid", result.Error!.Code);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Rejects_registration_that_does_not_accept_every_current_document()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var handler = Handler(db, new FakeEmailSender());
        var request = RegistrationCatalog.Request(catalog, 20) with { AcceptedDocumentIds = [catalog.DocumentIds[0]] };

        var result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("legal.documents_not_accepted", result.Error!.Code);
        Assert.Empty(db.Users);
        Assert.Empty(db.ConsentRecords);
    }

    [Fact]
    public async Task Rejects_a_city_from_another_department()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var handler = Handler(db, new FakeEmailSender());
        var request = RegistrationCatalog.Request(catalog, 20) with { CityId = catalog.OtherCityId };

        var result = await handler.Handle(request, CancellationToken.None);

        Assert.Equal("geography.city_unavailable", result.Error!.Code);
    }

    [Fact]
    public async Task Rejects_a_duplicate_email()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var handler = Handler(db, new FakeEmailSender());
        Assert.True((await handler.Handle(RegistrationCatalog.Request(catalog, 20), CancellationToken.None)).IsSuccess);

        var again = await handler.Handle(
            RegistrationCatalog.Request(catalog, 20, email: "ANA@situ.test") with { UserName = "otra" },
            CancellationToken.None);

        Assert.Equal("user.email_taken", again.Error!.Code);
    }

    private static RegisterUserHandler Handler(IdentityTestDb db, FakeEmailSender email) =>
        new(db, new FixedClock(RegistrationCatalog.Now), new FakePasswords(), email, new FakeLinks());
}

public class ConfirmEmailHandlerTests
{
    [Fact]
    public async Task Confirming_the_token_activates_an_adult_and_consumes_the_link()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var registered = await new RegisterUserHandler(db, clock, new FakePasswords(), email, new FakeLinks())
            .Handle(RegistrationCatalog.Request(catalog, 20), CancellationToken.None);
        var token = RegistrationCatalog.TokenFrom(email.Messages[0].Body);

        var result = await new ConfirmEmailHandler(db, clock).Handle(new ConfirmEmailRequest(token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(registered.Value!.UserId, result.Value!.UserId);
        Assert.Equal(UserStatus.Active, result.Value.Status);
        Assert.NotNull((await db.Users.SingleAsync()).EmailConfirmedAt);
        Assert.False((await db.SecurityTokens.SingleAsync()).IsValid(clock.UtcNow));
    }

    [Fact]
    public async Task Confirming_the_token_keeps_a_minor_waiting_for_a_guardian()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await new RegisterUserHandler(db, clock, new FakePasswords(), email, new FakeLinks())
            .Handle(RegistrationCatalog.Request(catalog, 14, email: "menor@situ.test"), CancellationToken.None);

        var result = await new ConfirmEmailHandler(db, clock).Handle(
            new ConfirmEmailRequest(RegistrationCatalog.TokenFrom(email.Messages[0].Body)),
            CancellationToken.None);

        Assert.Equal(UserStatus.PendingGuardianConsent, result.Value!.Status);
        Assert.NotNull((await db.Users.SingleAsync()).EmailConfirmedAt);
    }

    [Fact]
    public async Task Rejects_an_expired_token()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        await new RegisterUserHandler(db, clock, new FakePasswords(), email, new FakeLinks())
            .Handle(RegistrationCatalog.Request(catalog, 20), CancellationToken.None);
        clock.UtcNow = clock.UtcNow.Add(EmailConfirmationPolicy.TokenLifetime);

        var result = await new ConfirmEmailHandler(db, clock).Handle(
            new ConfirmEmailRequest(RegistrationCatalog.TokenFrom(email.Messages[0].Body)),
            CancellationToken.None);

        Assert.Equal("security_token.expired", result.Error!.Code);
        Assert.Null((await db.Users.SingleAsync()).EmailConfirmedAt);
    }
}

public class ResendConfirmationEmailHandlerTests
{
    [Fact]
    public async Task Resend_is_refused_inside_the_interval_and_allowed_after_it()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var links = new FakeLinks();
        var passwords = new FakePasswords();
        await new RegisterUserHandler(db, clock, passwords, email, links)
            .Handle(RegistrationCatalog.Request(catalog, 20), CancellationToken.None);
        var firstToken = RegistrationCatalog.TokenFrom(email.Messages[0].Body);
        var resend = new ResendConfirmationEmailHandler(db, clock, email, links);

        var tooSoon = await resend.Handle(new ResendConfirmationEmailRequest("ana@situ.test"), CancellationToken.None);

        Assert.Equal("auth.confirmation_rate_limited", tooSoon.Error!.Code);
        Assert.Single(email.Messages);

        clock.UtcNow = clock.UtcNow.Add(EmailConfirmationPolicy.MinimumResendInterval);
        var sent = await resend.Handle(new ResendConfirmationEmailRequest("ana@situ.test"), CancellationToken.None);

        Assert.True(sent.IsSuccess);
        Assert.Equal(2, email.Messages.Count);
        var confirm = new ConfirmEmailHandler(db, clock);
        Assert.Equal("security_token.already_used", (await confirm.Handle(new ConfirmEmailRequest(firstToken), CancellationToken.None)).Error!.Code);
        Assert.True((await confirm.Handle(new ConfirmEmailRequest(RegistrationCatalog.TokenFrom(email.Messages[1].Body)), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Unknown_address_does_not_send_mail()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var result = await new ResendConfirmationEmailHandler(db, new FixedClock(RegistrationCatalog.Now), email, new FakeLinks())
            .Handle(new ResendConfirmationEmailRequest("nadie@situ.test"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(email.Messages);
    }
}
