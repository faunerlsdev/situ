using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Moderation;

public sealed class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.Property(e => e.Details).HasMaxLength(1000);
        builder.Property(e => e.Resolution).HasMaxLength(1000);
        builder.Required<Complaint, User>(e => e.ReporterId);
        builder.Optional<Complaint, User>(e => e.ResolvedByStaffId);
    }
}
