using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Geography;
using Situ.Domain.Legal;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Legal;

public sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.Property(e => e.Locale).HasMaxLength(15);
        builder.Property(e => e.Version).HasMaxLength(30);
        builder.Optional<LegalDocument, Country>(e => e.CountryId);
        builder.HasIndex(e => new { e.Type, e.CountryId, e.Locale, e.Version }).IsUnique().AreNullsDistinct(false);
    }
}
