using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Guardianship;

namespace Situ.Infrastructure.Persistence.Configurations.Guardianship;

public sealed class GuardianConsentExpiryAuditConfiguration : IEntityTypeConfiguration<GuardianConsentExpiryAudit>
{
    public void Configure(EntityTypeBuilder<GuardianConsentExpiryAudit> builder)
    {
        builder.HasIndex(e => e.ConsentId).IsUnique();
    }
}
