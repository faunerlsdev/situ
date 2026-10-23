using Situ.Domain.Common;
using Situ.Domain.Guardianship;

namespace Situ.Domain.Tests.Guardianship;

public class MinorProtectionPolicyTests
{
    [Theory]
    [InlineData(13, true, false, false, true, false, false)]
    [InlineData(14, true, false, false, true, false, false)]
    [InlineData(15, true, false, false, true, false, false)]
    [InlineData(16, true, false, false, false, false, false)]
    [InlineData(17, true, false, false, false, false, false)]
    [InlineData(18, false, true, true, false, true, true)]
    [InlineData(30, false, true, true, false, true, true)]
    public void Age_gates_match_the_minor_protection_rules(
        int age,
        bool isMinor,
        bool publicProfile,
        bool publicStream,
        bool guardianRequiredToStream,
        bool adultCategories,
        bool personalizedAds)
    {
        Assert.Equal(18, MinorProtectionPolicy.AdultAge);
        Assert.Equal(16, MinorProtectionPolicy.LiveStreamingGuardianAuthorizationAgeLimit);
        Assert.Equal(isMinor, MinorProtectionPolicy.IsMinor(age));
        Assert.Equal(publicProfile, MinorProtectionPolicy.CanHavePublicProfile(age));
        Assert.Equal(publicStream, MinorProtectionPolicy.CanStreamTo(age, ContentVisibility.Public));
        Assert.True(MinorProtectionPolicy.CanStreamTo(age, ContentVisibility.FriendsOnly));
        Assert.Equal(guardianRequiredToStream, MinorProtectionPolicy.RequiresGuardianAuthorizationToStream(age));
        Assert.Equal(adultCategories, MinorProtectionPolicy.CanUseAdultOnlyCategories(age));
        Assert.Equal(personalizedAds, MinorProtectionPolicy.CanReceivePersonalizedAds(age));
    }
}
