using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Moderation;

public sealed class AnonymousIdentityAccessLogConfiguration : IEntityTypeConfiguration<AnonymousIdentityAccessLog>
{
    public void Configure(EntityTypeBuilder<AnonymousIdentityAccessLog> builder)
    {
        builder.Property(e => e.Justification).HasMaxLength(1000);
        builder.Required<AnonymousIdentityAccessLog, User>(e => e.StaffId);
        builder.Optional<AnonymousIdentityAccessLog, Complaint>(e => e.ComplaintId);
    }
}
