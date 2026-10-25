using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Administration;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Administration;

public sealed class StaffAssignmentConfiguration : IEntityTypeConfiguration<StaffAssignment>
{
    public void Configure(EntityTypeBuilder<StaffAssignment> builder)
    {
        builder.Required<StaffAssignment, User>(e => e.UserId);
        builder.Required<StaffAssignment, StaffRole>(e => e.RoleId);
        builder.Optional<StaffAssignment, User>(e => e.GrantedByUserId);
        builder.HasIndex(e => new { e.UserId, e.IsActive });
    }
}
