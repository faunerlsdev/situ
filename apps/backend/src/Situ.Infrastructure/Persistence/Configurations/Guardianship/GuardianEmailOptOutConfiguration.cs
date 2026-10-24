using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Guardianship;

namespace Situ.Infrastructure.Persistence.Configurations.Guardianship;

public sealed class GuardianEmailOptOutConfiguration : IEntityTypeConfiguration<GuardianEmailOptOut>
{
    public void Configure(EntityTypeBuilder<GuardianEmailOptOut> builder)
    {
        builder.Property(e => e.Email).HasMaxLength(254);
        builder.HasIndex(e => e.Email).IsUnique();
    }
}
