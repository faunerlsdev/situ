using Situ.Domain.News;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.News;

public class ImportanceScoringConfigTests
{
    [Fact]
    public void Calculate_decays_by_half_life_and_ShouldPromote_uses_the_threshold()
    {
        var config = ImportanceScoringConfig.CreateDefault(TestData.Now);

        Assert.Equal(120, config.HalfLifeMinutes);
        Assert.Equal(12, config.PromotionThreshold);

        var fresh = config.Calculate(
            categoryWeight: 8,
            stillThereVotes: 4,
            comments: 2,
            reactions: 4,
            authorReputation: 2,
            hasMedia: true,
            age: TimeSpan.Zero);

        var halfLife = config.Calculate(
            categoryWeight: 8,
            stillThereVotes: 4,
            comments: 2,
            reactions: 4,
            authorReputation: 2,
            hasMedia: true,
            age: TimeSpan.FromMinutes(config.HalfLifeMinutes));

        Assert.Equal(18, fresh);
        Assert.Equal(9, halfLife);
        Assert.True(config.ShouldPromote(fresh));
        Assert.True(config.ShouldPromote(config.PromotionThreshold));
        Assert.False(config.ShouldPromote(halfLife));
        Assert.False(config.ShouldPromote(config.PromotionThreshold - 0.01));
    }
}
