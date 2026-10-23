using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class ReportConfirmationConfiguration : IEntityTypeConfiguration<ReportConfirmation>
{
    public void Configure(EntityTypeBuilder<ReportConfirmation> builder)
    {
        builder.Required<ReportConfirmation, Report>(e => e.ReportId);
        builder.Required<ReportConfirmation, User>(e => e.UserId);
        builder.HasIndex(e => new { e.ReportId, e.UserId }).IsUnique().AreNullsDistinct(false);
    }
}
