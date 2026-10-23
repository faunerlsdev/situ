using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Profiles;

namespace Situ.Infrastructure.Persistence.Configurations.Profiles;

public sealed class AvatarGalleryItemConfiguration : IEntityTypeConfiguration<AvatarGalleryItem>
{
    public void Configure(EntityTypeBuilder<AvatarGalleryItem> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(60);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
    }
}
