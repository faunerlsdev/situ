using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Reports;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.Reports;

public class ReportConfirmationTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void Still_there_extends_the_lifetime_and_gone_does_not()
    {
        var report = Create(TimeSpan.FromHours(2));
        var originalExpiry = report.ExpiresAt;

        report.RegisterConfirmation(ConfirmationType.StillThere, TimeSpan.FromMinutes(30), Now.AddMinutes(10));

        Assert.Equal(1, report.StillThereCount);
        Assert.Equal(0, report.GoneNowCount);
        Assert.Equal(originalExpiry.AddMinutes(30), report.ExpiresAt);

        report.RegisterConfirmation(ConfirmationType.GoneNow, TimeSpan.FromHours(5), Now.AddMinutes(20));

        Assert.Equal(1, report.GoneNowCount);
        Assert.Equal(originalExpiry.AddMinutes(30), report.ExpiresAt);
    }

    [Fact]
    public void Still_there_after_the_previous_expiry_extends_from_now()
    {
        var report = Create(TimeSpan.FromHours(1));
        var confirmedAt = report.ExpiresAt.AddMinutes(5);

        report.RegisterConfirmation(ConfirmationType.StillThere, TimeSpan.FromMinutes(20), confirmedAt);

        Assert.Equal(confirmedAt.AddMinutes(20), report.ExpiresAt);
        Assert.True(report.IsVisible(confirmedAt));
    }

    [Fact]
    public void Replacing_a_vote_does_not_count_it_twice()
    {
        var report = Create(TimeSpan.FromHours(2));
        report.ReplaceConfirmation(null, ConfirmationType.StillThere, TimeSpan.FromMinutes(10), Now);
        report.ReplaceConfirmation(ConfirmationType.StillThere, ConfirmationType.GoneNow, TimeSpan.FromMinutes(10), Now.AddMinutes(1));

        Assert.Equal(0, report.StillThereCount);
        Assert.Equal(1, report.GoneNowCount);
    }

    [Fact]
    public void A_minor_public_report_must_be_anonymous()
    {
        Assert.False(MinorProtectionPolicy.CanPublishReport(16, ContentVisibility.Public, false));
        Assert.True(MinorProtectionPolicy.CanPublishReport(16, ContentVisibility.Public, true));
        Assert.True(MinorProtectionPolicy.CanPublishReport(16, ContentVisibility.FriendsOnly, false));
        Assert.True(MinorProtectionPolicy.CanPublishReport(30, ContentVisibility.Public, false));
    }

    private static Report Create(TimeSpan lifetime) =>
        Report.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new GeoPoint(4.7110, -74.0721),
            "Hay un retén en la esquina.",
            isAnonymous: false,
            authorIsMinor: false,
            lifetime,
            Now);
}
