using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Profiles;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.Property(e => e.DisplayName).HasMaxLength(60);
        builder.Property(e => e.PreferAnonymousReports).HasDefaultValue(true);
        builder.Property(e => e.Bio).HasMaxLength(UserProfile.BioMaxLength);
        builder.Property(e => e.AvatarUrl).HasMaxLength(500);
        builder.Required<UserProfile, User>(e => e.UserId);
        builder.Optional<UserProfile, AvatarGalleryItem>(e => e.AvatarGalleryItemId);
        builder.HasIndex(e => e.UserId).IsUnique().AreNullsDistinct(false);
    }
}
