using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Organizations;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Organizations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.Required<OrganizationMember, Organization>(e => e.OrganizationId);
        builder.Required<OrganizationMember, User>(e => e.UserId);
        builder.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique().AreNullsDistinct(false);
    }
}
