using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.Guardianship;

public class GuardianConsentTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void Request_then_data_email_and_authorize_completes_the_flow()
    {
        var consent = Request();

        Assert.Equal(GuardianConsentStatus.Requested, consent.Status);
        Assert.Equal(Now.Add(MinorProtectionPolicy.DefaultGuardianResponseWindow), consent.ExpiresAt);
        Assert.True(consent.IsOpen(Now));

        var missingData = Assert.Throws<DomainException>(() => consent.VerifyGuardianEmail(Now));
        Assert.Equal("guardian_consent.data_missing", missingData.Code);

        var notVerified = Assert.Throws<DomainException>(() => Authorize(consent, Now));
        Assert.Equal("guardian_consent.email_not_verified", notVerified.Code);

        consent.SubmitGuardianData(
            "Carlos Pérez",
            GuardianDocumentType.CitizenshipCard,
            "1234567890",
            "carlos@correo.com",
            Now.AddMinutes(5));

        Assert.Equal(GuardianConsentStatus.Requested, consent.Status);
        Assert.Equal("Carlos Pérez", consent.GuardianFullName);
        Assert.Equal("carlos@correo.com", consent.GuardianContactEmail);
        Assert.Null(consent.GuardianEmailVerifiedAt);

        var verifiedAt = Now.AddMinutes(10);
        consent.VerifyGuardianEmail(verifiedAt);

        Assert.Equal(GuardianConsentStatus.EmailVerified, consent.Status);
        Assert.Equal(verifiedAt, consent.GuardianEmailVerifiedAt);

        var authorizedAt = Now.AddMinutes(15);
        Authorize(consent, authorizedAt);

        Assert.Equal(GuardianConsentStatus.Authorized, consent.Status);
        Assert.Equal(authorizedAt, consent.AuthorizedAt);
        Assert.Equal("signature", consent.DigitalSignature);
        Assert.True(consent.LiveStreamingAuthorized);
        Assert.True(consent.LocationSharingAuthorized);
        Assert.False(consent.IsOpen(authorizedAt));
        Assert.Contains(consent.DomainEvents, e => e is GuardianConsentAuthorizedEvent);

        Assert.False(consent.ExpireIfDue(Now.AddDays(8)));
        Assert.Equal(GuardianConsentStatus.Authorized, consent.Status);
    }

    [Fact]
    public void Unanswered_request_expires_after_7_days()
    {
        var consent = Request();
        var lastOpenMoment = consent.ExpiresAt.AddTicks(-1);

        Assert.Equal(TimeSpan.FromDays(7), MinorProtectionPolicy.DefaultGuardianResponseWindow);
        Assert.True(consent.IsOpen(lastOpenMoment));
        Assert.False(consent.ExpireIfDue(lastOpenMoment));

        Assert.False(consent.IsOpen(consent.ExpiresAt));
        Assert.True(consent.ExpireIfDue(consent.ExpiresAt));
        Assert.Equal(GuardianConsentStatus.Expired, consent.Status);

        var expired = Assert.Throws<DomainException>(() => consent.SubmitGuardianData(
            "Carlos Pérez",
            GuardianDocumentType.CitizenshipCard,
            "1234567890",
            "carlos@correo.com",
            consent.ExpiresAt));
        Assert.Equal("guardian_consent.not_open", expired.Code);
    }

    [Fact]
    public void Open_request_cannot_be_completed_after_the_window()
    {
        var consent = Request();
        consent.SubmitGuardianData(
            "Carlos Pérez",
            GuardianDocumentType.CitizenshipCard,
            "1234567890",
            "carlos@correo.com",
            Now.AddMinutes(1));
        consent.VerifyGuardianEmail(Now.AddMinutes(2));

        var expired = Assert.Throws<DomainException>(() => Authorize(consent, consent.ExpiresAt));
        Assert.Equal("guardian_consent.expired", expired.Code);
        Assert.Equal(GuardianConsentStatus.EmailVerified, consent.Status);
    }

    private static GuardianConsent Request() =>
        GuardianConsent.Request(
            Guid.CreateVersion7(),
            "tutor@situ.test",
            "token-hash",
            "privacy-2026",
            Now.AddMinutes(-1),
            Now);

    private static void Authorize(GuardianConsent consent, DateTimeOffset now) =>
        consent.Authorize("signature", liveStreamingAuthorized: true, locationSharingAuthorized: true, "127.0.0.1", "SituTests", now);
}
