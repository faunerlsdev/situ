using Situ.Domain.Common;

namespace Situ.Domain.News;

/// <summary>
/// Parameters of the "important news" algorithm. Editable from the admin panel (RF-NOT-07, RF-ADM-06).
/// score = categoryWeight*W1 + confirmations*W2 + comments*W3 + reactions*W4 + reputation*W5 + media bonus, decayed by age.
/// </summary>
public sealed class ImportanceScoringConfig : AuditableEntity
{
    private ImportanceScoringConfig()
    {
    }

    public Guid? CountryId { get; private set; }
    public double CategoryWeightFactor { get; private set; }
    public double ConfirmationFactor { get; private set; }
    public double CommentFactor { get; private set; }
    public double ReactionFactor { get; private set; }
    public double ReputationFactor { get; private set; }
    public double MediaBonus { get; private set; }

    /// <summary>Score halves every this many minutes.</summary>
    public double HalfLifeMinutes { get; private set; }

    /// <summary>Minimum score for a report to be promoted to important news.</summary>
    public double PromotionThreshold { get; private set; }

    public bool IsActive { get; private set; }

    public static ImportanceScoringConfig CreateDefault(DateTimeOffset now)
    {
        var config = new ImportanceScoringConfig
        {
            CategoryWeightFactor = 1.0,
            ConfirmationFactor = 1.5,
            CommentFactor = 0.5,
            ReactionFactor = 0.25,
            ReputationFactor = 0.5,
            MediaBonus = 1.0,
            HalfLifeMinutes = 120,
            PromotionThreshold = 12,
            IsActive = true,
        };

        config.MarkCreated(now);
        return config;
    }

    public void Update(
        double categoryWeightFactor,
        double confirmationFactor,
        double commentFactor,
        double reactionFactor,
        double reputationFactor,
        double mediaBonus,
        double halfLifeMinutes,
        double promotionThreshold,
        DateTimeOffset now)
    {
        CategoryWeightFactor = Guard.InRange(categoryWeightFactor, 0d, 100d, nameof(categoryWeightFactor));
        ConfirmationFactor = Guard.InRange(confirmationFactor, 0d, 100d, nameof(confirmationFactor));
        CommentFactor = Guard.InRange(commentFactor, 0d, 100d, nameof(commentFactor));
        ReactionFactor = Guard.InRange(reactionFactor, 0d, 100d, nameof(reactionFactor));
        ReputationFactor = Guard.InRange(reputationFactor, 0d, 100d, nameof(reputationFactor));
        MediaBonus = Guard.InRange(mediaBonus, 0d, 100d, nameof(mediaBonus));
        HalfLifeMinutes = Guard.InRange(halfLifeMinutes, 1d, 60d * 24 * 7, nameof(halfLifeMinutes));
        PromotionThreshold = Guard.InRange(promotionThreshold, 0d, 10_000d, nameof(promotionThreshold));
        MarkUpdated(now);
    }

    public double Calculate(
        double categoryWeight,
        int stillThereVotes,
        int comments,
        int reactions,
        double authorReputation,
        bool hasMedia,
        TimeSpan age)
    {
        var raw = categoryWeight * CategoryWeightFactor
                  + stillThereVotes * ConfirmationFactor
                  + comments * CommentFactor
                  + reactions * ReactionFactor
                  + authorReputation * ReputationFactor
                  + (hasMedia ? MediaBonus : 0);

        var decay = Math.Pow(0.5, Math.Max(0, age.TotalMinutes) / HalfLifeMinutes);
        return raw * decay;
    }

    public bool ShouldPromote(double score) => IsActive && score >= PromotionThreshold;
}
