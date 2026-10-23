using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.Identity;

public class UserRegistrationTests
{
    [Fact]
    public void Register_rejects_age_13_and_accepts_14_as_pending_guardian_consent()
    {
        var country = Country.Create(
            "CO",
            "Colombia",
            "Departamento",
            "es-CO",
            "COP",
            "America/Bogota",
            DistanceUnit.Kilometers,
            minimumAge: 14,
            isEnabled: true);

        var tooYoung = Assert.Throws<DomainException>(() => TestData.UserAged(13, country.MinimumAge));
        Assert.Equal("user.below_minimum_age", tooYoung.Code);

        var minor = TestData.UserAged(14, country.MinimumAge);

        Assert.Equal(UserStatus.PendingGuardianConsent, minor.Status);
        Assert.True(minor.IsMinor(TestData.Now));
        Assert.Null(minor.GuardianConsentGrantedAt);
    }

    [Fact]
    public void Minor_cannot_sign_in_until_the_guardian_authorizes()
    {
        var minor = TestData.UserAged(14);

        Assert.Equal(SignInEligibility.PendingGuardianConsent, minor.EvaluateSignIn(TestData.Now));

        var authorizedAt = TestData.Now.AddHours(1);
        minor.GrantGuardianConsent(authorizedAt);

        Assert.Equal(authorizedAt, minor.GuardianConsentGrantedAt);
        Assert.Equal(SignInEligibility.Allowed, minor.EvaluateSignIn(authorizedAt));

        minor.RevokeGuardianConsent(authorizedAt.AddDays(1));

        Assert.Null(minor.GuardianConsentGrantedAt);
        Assert.Equal(UserStatus.PendingGuardianConsent, minor.Status);
        Assert.Equal(SignInEligibility.PendingGuardianConsent, minor.EvaluateSignIn(authorizedAt.AddDays(1)));
    }
}
