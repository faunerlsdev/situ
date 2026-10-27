using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Guardianship.AuthorizeGuardianConsent;
using Situ.Application.Features.Guardianship.ChangeGuardianEmail;
using Situ.Application.Features.Guardianship.DeclineGuardianConsent;
using Situ.Application.Features.Guardianship.ExpireGuardianConsents;
using Situ.Application.Features.Guardianship.GetGuardianConsentForReview;
using Situ.Application.Features.Guardianship.GetMyGuardianConsentStatus;
using Situ.Application.Features.Guardianship.OptOutGuardianEmail;
using Situ.Application.Features.Guardianship.RequestGuardianConsent;
using Situ.Application.Features.Guardianship.ResendGuardianConsentEmail;
using Situ.Application.Features.Guardianship.ResendGuardianEmailCode;
using Situ.Application.Features.Guardianship.RevokeGuardianConsent;
using Situ.Application.Features.Guardianship.SubmitGuardianData;
using Situ.Application.Features.Guardianship.VerifyGuardianEmailCode;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Retention;

namespace Situ.Application.Tests.Guardianship;

public class GuardianConsentFlowTests
{
    [Fact]
    public async Task A_minor_without_a_guardian_email_can_request_one_later_and_then_be_authorized()
    {
        await using var db = IdentityTestDb.Create();
        var email = new FakeEmailSender();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -15);
        var current = new FixedUser(user.Id);

        var before = await Status(db, clock, current).Handle(new GetMyGuardianConsentStatusRequest(), CancellationToken.None);
        var requested = await Request(db, clock, current, email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true, "Quiero usarla para avisar lo que pasa en mi barrio."),
            CancellationToken.None);
        var review = await new GetGuardianConsentForReviewHandler(db, clock).Handle(
            new GetGuardianConsentForReviewRequest(Token(email.Messages[0].Body)),
            CancellationToken.None);
        await Submit(db, clock, email).Handle(Data(Token(email.Messages[0].Body)), CancellationToken.None);
        var code = CodeIn(email.Messages[1].Body);
        var verified = await new VerifyGuardianEmailCodeHandler(db, clock).Handle(
            new VerifyGuardianEmailCodeRequest(Token(email.Messages[0].Body), code),
            CancellationToken.None);
        var authorized = await Authorize(db, clock, email).Handle(
            new AuthorizeGuardianConsentRequest(Token(email.Messages[0].Body), false, false, "203.0.113.9", "tests", true),
            CancellationToken.None);

        Assert.Equal("NotRequested", before.Value!.Status);
        Assert.False(before.Value.MinorNoticeRecorded);
        Assert.True(requested.IsSuccess);
        Assert.Equal("Requested", requested.Value!.Status);
        Assert.Equal("t***@situ.test", requested.Value.MaskedEmail);
        Assert.Equal("menor", review.Value!.UserName);
        Assert.Equal(15, review.Value.Age);
        Assert.True(review.Value.RequiresLiveAuthorization);
        Assert.Contains("barrio", review.Value.MinorStatement);
        Assert.True(verified.IsSuccess);
        Assert.True(authorized.IsSuccess);
        Assert.Equal(UserStatus.Active, (await db.Users.SingleAsync()).Status);
        Assert.False((await db.GuardianConsents.SingleAsync()).LiveStreamingAuthorized);
        Assert.Equal("web", (await db.GuardianConsents.SingleAsync()).AuthorizationChannel);
        Assert.True(MinorProtectionPolicy.RequiresGuardianAuthorizationToStream(15));
    }

    [Fact]
    public async Task An_unknown_expired_or_used_review_token_returns_the_same_message()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -15);
        var email = new FakeEmailSender();
        await Request(db, clock, new FixedUser(user.Id), email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true),
            CancellationToken.None);
        var token = Token(email.Messages[0].Body);
        var handler = new GetGuardianConsentForReviewHandler(db, clock);

        var unknown = await handler.Handle(new GetGuardianConsentForReviewRequest("no-existe"), CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddDays(8);
        var expired = await handler.Handle(new GetGuardianConsentForReviewRequest(token), CancellationToken.None);

        Assert.Equal("guardian_consent.not_found", unknown.Error!.Code);
        Assert.Equal(unknown.Error.Message, expired.Error!.Message);
    }

    [Fact]
    public async Task Five_wrong_codes_invalidate_the_code()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -15);
        await Request(db, clock, new FixedUser(user.Id), email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true),
            CancellationToken.None);
        var token = Token(email.Messages[0].Body);
        await Submit(db, clock, email).Handle(Data(token), CancellationToken.None);
        var verify = new VerifyGuardianEmailCodeHandler(db, clock);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await verify.Handle(new VerifyGuardianEmailCodeRequest(token, "000000"), CancellationToken.None);
            Assert.Equal("guardian_consent.code_invalid", wrong.Error!.Code);
        }

        var after = await verify.Handle(new VerifyGuardianEmailCodeRequest(token, CodeIn(email.Messages[1].Body)), CancellationToken.None);
        Assert.Equal("guardian_consent.code_invalid", after.Error!.Code);
    }

    [Fact]
    public async Task Declining_notifies_the_minor_and_blocks_a_new_request()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -15);
        var current = new FixedUser(user.Id);
        await Request(db, clock, current, email).Handle(new RequestGuardianConsentRequest("tutor@situ.test", true), CancellationToken.None);

        var declined = await new DeclineGuardianConsentHandler(db, clock, email).Handle(
            new DeclineGuardianConsentRequest(Token(email.Messages[0].Body), "No autorizo"),
            CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        var again = await Request(db, clock, current, email).Handle(
            new RequestGuardianConsentRequest("otro@situ.test", true),
            CancellationToken.None);

        Assert.True(declined.IsSuccess);
        Assert.Equal("guardian_consent.declined", again.Error!.Code);
        Assert.Contains(email.Messages, message => message.To == "menor@situ.test" && message.Subject.Contains("no autorizó"));
    }

    [Fact]
    public async Task The_guardian_can_revoke_from_the_receipt_link()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -16);
        await AuthorizeFully(db, clock, email, user);
        var manage = Token(email.Messages.Last(message => message.Subject.Contains("Constancia")).Body);

        var revoked = await new RevokeGuardianConsentHandler(db, clock, email).Handle(
            new RevokeGuardianConsentRequest(manage),
            CancellationToken.None);
        var again = await new RevokeGuardianConsentHandler(db, clock, email).Handle(
            new RevokeGuardianConsentRequest(manage),
            CancellationToken.None);

        Assert.True(revoked.IsSuccess);
        Assert.Equal("guardian_consent.not_found", again.Error!.Code);
        Assert.Equal(UserStatus.PendingGuardianConsent, (await db.Users.SingleAsync()).Status);
        Assert.Equal(GuardianConsentStatus.Revoked, (await db.GuardianConsents.SingleAsync()).Status);
    }

    [Fact]
    public async Task Expiry_after_seven_days_erases_the_minor_and_keeps_an_audit_without_personal_data()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -15);
        db.GuardianConsents.Add(GuardianConsent.Request(user.Id, "tutor@situ.test", Hash("drop"), "2026-10", clock.UtcNow, clock.UtcNow));
        await db.SaveChangesAsync();
        clock.UtcNow = clock.UtcNow.AddDays(7);

        var due = await new ExpireGuardianConsentsHandler(db, clock, new FakeEmailSender())
            .Handle(new ExpireGuardianConsentsRequest(), CancellationToken.None);

        var stored = await db.Users.SingleAsync();
        var audit = await db.GuardianConsentExpiryAudits.SingleAsync();
        Assert.Equal(1, due.Value!.Expired);
        Assert.Equal(UserStatus.Deleted, stored.Status);
        Assert.DoesNotContain("menor@", stored.Email.Value);
        Assert.True(audit.PersonalDataDeleted);
        Assert.Equal(GuardianConsentStatus.Expired, (await db.GuardianConsents.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_minor_cannot_send_more_than_five_emails_in_a_day_or_sooner_than_two_minutes()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -15);
        var current = new FixedUser(user.Id);
        var request = Request(db, clock, current, email);
        var resend = new ResendGuardianConsentEmailHandler(db, clock, current, email, new FakeLinks());
        await request.Handle(new RequestGuardianConsentRequest("tutor@situ.test", true), CancellationToken.None);

        var tooSoon = await resend.Handle(new ResendGuardianConsentEmailRequest("203.0.113.10"), CancellationToken.None);
        Assert.Equal("guardian_consent.send_limited", tooSoon.Error!.Code);

        var change = new ChangeGuardianEmailHandler(db, clock, current, email, new FakeLinks());
        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        Assert.True((await resend.Handle(new ResendGuardianConsentEmailRequest("203.0.113.10"), CancellationToken.None)).IsSuccess);
        foreach (var index in new[] { 1, 2, 3 })
        {
            clock.UtcNow = clock.UtcNow.AddMinutes(3);
            var changed = await change.Handle(new ChangeGuardianEmailRequest($"tutor{index}@situ.test", "203.0.113.10"), CancellationToken.None);
            Assert.True(changed.IsSuccess, changed.Error?.Code);
        }

        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        var overChanges = await change.Handle(new ChangeGuardianEmailRequest("tutor4@situ.test", "203.0.113.10"), CancellationToken.None);
        Assert.Equal("guardian_consent.send_limited", overChanges.Error!.Code);

        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        var blocked = await resend.Handle(new ResendGuardianConsentEmailRequest("203.0.113.10"), CancellationToken.None);
        Assert.Equal("guardian_consent.send_limited", blocked.Error!.Code);
        Assert.True(blocked.Error.Extensions.ContainsKey("retryAfter"));
    }

    [Fact]
    public async Task The_same_destination_accepts_three_requests_per_hour_and_then_waits()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        for (var index = 0; index < 3; index++)
        {
            var user = await MinorAsync(db, clock, $"m{index}@situ.test", $"menor{index}", -15);
            var result = await Request(db, clock, new FixedUser(user.Id), email).Handle(
                new RequestGuardianConsentRequest("tutor@situ.test", true, null, $"203.0.113.{index}"),
                CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        var extra = await MinorAsync(db, clock, "extra@situ.test", "extra", -15);
        var blocked = await Request(db, clock, new FixedUser(extra.Id), email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true, null, "203.0.113.40"),
            CancellationToken.None);
        Assert.Equal("guardian_consent.send_limited", blocked.Error!.Code);
    }

    [Fact]
    public async Task One_ip_accepts_ten_requests_per_hour()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        for (var index = 0; index < 10; index++)
        {
            var user = await MinorAsync(db, clock, $"ip{index}@situ.test", $"ip{index}", -15);
            var result = await Request(db, clock, new FixedUser(user.Id), email).Handle(
                new RequestGuardianConsentRequest($"tutor{index}@situ.test", true, null, "198.51.100.8"),
                CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        var extra = await MinorAsync(db, clock, "ipx@situ.test", "ipx", -15);
        var blocked = await Request(db, clock, new FixedUser(extra.Id), email).Handle(
            new RequestGuardianConsentRequest("otro@situ.test", true, null, "198.51.100.8"),
            CancellationToken.None);
        Assert.Equal("guardian_consent.send_limited", blocked.Error!.Code);
    }

    [Fact]
    public async Task A_verification_code_can_be_resent_only_three_times_in_fifteen_minutes()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        var user = await MinorAsync(db, clock, "menor@situ.test", "menor", -15);
        await Request(db, clock, new FixedUser(user.Id), email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true),
            CancellationToken.None);
        var token = Token(email.Messages[0].Body);
        await Submit(db, clock, email).Handle(Data(token), CancellationToken.None);
        var resend = new ResendGuardianEmailCodeHandler(db, clock, email);

        Assert.True((await resend.Handle(new ResendGuardianEmailCodeRequest(token), CancellationToken.None)).IsSuccess);
        Assert.True((await resend.Handle(new ResendGuardianEmailCodeRequest(token), CancellationToken.None)).IsSuccess);
        Assert.True((await resend.Handle(new ResendGuardianEmailCodeRequest(token), CancellationToken.None)).IsSuccess);
        var blocked = await resend.Handle(new ResendGuardianEmailCodeRequest(token), CancellationToken.None);
        Assert.Equal("guardian_consent.send_limited", blocked.Error!.Code);
    }

    [Fact]
    public async Task An_opted_out_address_is_rejected_without_saying_why()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(RegistrationCatalog.Now);
        var email = new FakeEmailSender();
        var first = await MinorAsync(db, clock, "uno@situ.test", "uno", -15);
        await Request(db, clock, new FixedUser(first.Id), email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true),
            CancellationToken.None);
        var opted = await new OptOutGuardianEmailHandler(db, clock).Handle(
            new OptOutGuardianEmailRequest(Token(email.Messages[0].Body)),
            CancellationToken.None);
        var second = await MinorAsync(db, clock, "dos@situ.test", "dos", -15);
        var blocked = await Request(db, clock, new FixedUser(second.Id), email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true),
            CancellationToken.None);

        Assert.True(opted.IsSuccess);
        Assert.Equal("guardian_consent.email_rejected", blocked.Error!.Code);
        Assert.DoesNotContain("exclus", blocked.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AuthorizeFully(IdentityTestDb db, FixedClock clock, FakeEmailSender email, User user)
    {
        await Request(db, clock, new FixedUser(user.Id), email).Handle(
            new RequestGuardianConsentRequest("tutor@situ.test", true),
            CancellationToken.None);
        var token = Token(email.Messages[0].Body);
        await Submit(db, clock, email).Handle(Data(token), CancellationToken.None);
        await new VerifyGuardianEmailCodeHandler(db, clock).Handle(
            new VerifyGuardianEmailCodeRequest(token, CodeIn(email.Messages[^1].Body)),
            CancellationToken.None);
        await Authorize(db, clock, email).Handle(
            new AuthorizeGuardianConsentRequest(token, true, false, null, null, true),
            CancellationToken.None);
    }

    private static RequestGuardianConsentHandler Request(IdentityTestDb db, FixedClock clock, FixedUser current, FakeEmailSender email) =>
        new(db, clock, current, email, new FakeLinks());

    private static GetMyGuardianConsentStatusHandler Status(IdentityTestDb db, FixedClock clock, FixedUser current) =>
        new(db, clock, current);

    private static SubmitGuardianDataHandler Submit(IdentityTestDb db, FixedClock clock, FakeEmailSender email) =>
        new(db, clock, email, new FakeLinks());

    private static AuthorizeGuardianConsentHandler Authorize(IdentityTestDb db, FixedClock clock, FakeEmailSender email) =>
        new(db, clock, new FakeSigner(), email, new FakeLinks());

    private static SubmitGuardianDataRequest Data(string token) =>
        new(token, "Carlos Pérez", GuardianDocumentType.CitizenshipCard, "1234567890", "carlos@correo.com");

    private static string Token(string body) => RegistrationCatalog.TokenFrom(body);

    private static string CodeIn(string body)
    {
        var match = Regex.Match(body, @"código en la página: (\d{6})");
        Assert.True(match.Success);
        return match.Groups[1].Value;
    }

    private static string Hash(string plain) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plain)));

    private static async Task<User> MinorAsync(IdentityTestDb db, FixedClock clock, string email, string userName, int years)
    {
        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+573001234567"),
            "hashed:Clave123",
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(years),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "es-CO",
            14,
            clock.UtcNow);
        user.ConfirmEmail(clock.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
        public Guid? DeviceId => null;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeSigner : IGuardianConsentSigner
    {
        public string Sign(GuardianConsentSignature input) => "sig:" + input.ConsentId.ToString("N");
    }
}
