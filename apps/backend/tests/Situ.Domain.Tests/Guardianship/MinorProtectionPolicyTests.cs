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
        Assert.Equal(age >= 18, MinorProtectionPolicy.CanUploadProfilePhoto(age));
    }

    [Fact]
    public void A_minor_uses_their_name_on_a_comment_only_when_the_report_is_friends_only()
    {
        Assert.False(MinorProtectionPolicy.CanComment(17, ContentVisibility.Public, false));
        Assert.True(MinorProtectionPolicy.CanComment(17, ContentVisibility.Public, true));
        Assert.True(MinorProtectionPolicy.CanComment(17, ContentVisibility.FriendsOnly, false));
        Assert.True(MinorProtectionPolicy.CanComment(17, ContentVisibility.FriendsOnly, true));
        Assert.True(MinorProtectionPolicy.CanComment(18, ContentVisibility.Public, false));
        Assert.True(MinorProtectionPolicy.CanComment(30, ContentVisibility.Public, false));
    }

    [Fact]
    public void Adults_and_minors_are_not_friends_unless_the_setting_allows_it()
    {
        Assert.False(MinorProtectionPolicy.CanBeFriends(17, 30, false));
        Assert.True(MinorProtectionPolicy.CanBeFriends(16, 17, false));
        Assert.True(MinorProtectionPolicy.CanBeFriends(20, 30, false));
        Assert.True(MinorProtectionPolicy.CanBeFriends(17, 30, true));
    }

    [Theory]
    [InlineData(17, false, false)]
    [InlineData(17, true, true)]
    [InlineData(18, false, true)]
    public void A_minor_shares_a_proximity_location_only_with_guardian_authorization(int age, bool guardianAuthorized, bool allowed)
    {
        Assert.Equal(allowed, MinorProtectionPolicy.CanShareProximityLocation(age, guardianAuthorized));
    }
}
