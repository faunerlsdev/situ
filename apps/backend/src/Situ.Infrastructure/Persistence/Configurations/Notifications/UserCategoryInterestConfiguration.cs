using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Notifications;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Notifications;

public sealed class UserCategoryInterestConfiguration : IEntityTypeConfiguration<UserCategoryInterest>
{
    public void Configure(EntityTypeBuilder<UserCategoryInterest> builder)
    {
        builder.Required<UserCategoryInterest, User>(e => e.UserId);
        builder.Required<UserCategoryInterest, ReportCategory>(e => e.CategoryId);
        builder.HasIndex(e => new { e.UserId, e.CategoryId }).IsUnique().AreNullsDistinct(false);
    }
}
