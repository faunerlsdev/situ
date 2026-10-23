using Situ.Domain.Common;
using Situ.Domain.Monetization;

namespace Situ.Domain.Tests.Monetization;

public class SubscriptionPlanLimitTests
{
    [Theory]
    [InlineData("free", PlanTier.Free, true, 2, 15, 10, 3, 0, false)]
    [InlineData("basic", PlanTier.Basic, false, 3, 60, 30, 15, 5, false)]
    [InlineData("pro", PlanTier.Pro, false, 5, 180, null, null, 30, true)]
    [InlineData("advanced", PlanTier.Advanced, false, null, null, null, null, null, true)]
    public void Plans_keep_finite_caps_and_treat_null_as_unlimited(
        string code,
        PlanTier tier,
        bool showsAds,
        int? maxDevices,
        int? maxLiveMinutes,
        int? maxReportsPerDay,
        int? maxAnonymousPostsPerDay,
        int? maxSavedRecordings,
        bool canSponsorPins)
    {
        var plan = SubscriptionPlan.Create(
            code.ToUpperInvariant(),
            code,
            "Plan de prueba",
            tier,
            sortOrder: (int)tier,
            showsAds,
            maxDevices,
            maxLiveMinutes,
            maxReportsPerDay,
            maxAnonymousPostsPerDay,
            maxSavedRecordings,
            canSponsorPins);

        Assert.Equal(code, plan.Code);
        Assert.Equal(showsAds, plan.ShowsAds);
        Assert.Equal(maxDevices, plan.MaxDevices);
        Assert.Equal(maxLiveMinutes, plan.MaxLiveMinutesPerStream);
        Assert.Equal(maxReportsPerDay, plan.MaxReportsPerDay);
        Assert.Equal(maxAnonymousPostsPerDay, plan.MaxAnonymousPostsPerDay);
        Assert.Equal(maxSavedRecordings, plan.MaxSavedRecordings);
        Assert.Equal(canSponsorPins, plan.CanSponsorPins);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Update_can_remove_a_cap_and_rejects_a_negative_limit()
    {
        var plan = SubscriptionPlan.Create(
            "free",
            "Free",
            "Con topes.",
            PlanTier.Free,
            0,
            showsAds: true,
            maxDevices: 2,
            maxLiveMinutesPerStream: 15,
            maxReportsPerDay: 10,
            maxAnonymousPostsPerDay: 3,
            maxSavedRecordings: 0,
            canSponsorPins: false);

        plan.Update("Free", "Sin tope de reportes.", 0, true, 2, 15, null, 3, 0, false);

        Assert.Null(plan.MaxReportsPerDay);
        Assert.Equal(2, plan.MaxDevices);

        var rejected = Assert.Throws<DomainException>(() => plan.Update(
            "Free",
            "Con tope inválido.",
            0,
            true,
            maxDevices: -1,
            maxLiveMinutesPerStream: 15,
            maxReportsPerDay: 10,
            maxAnonymousPostsPerDay: 3,
            maxSavedRecordings: 0,
            canSponsorPins: false));

        Assert.Equal("maxDevices.out_of_range", rejected.Code);
        Assert.Equal(2, plan.MaxDevices);
    }
}
