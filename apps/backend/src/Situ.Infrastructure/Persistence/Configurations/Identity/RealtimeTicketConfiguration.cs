using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Identity;

public sealed class RealtimeTicketConfiguration : IEntityTypeConfiguration<RealtimeTicket>
{
    public void Configure(EntityTypeBuilder<RealtimeTicket> builder)
    {
        builder.Required<RealtimeTicket, User>(ticket => ticket.UserId);
        builder.HasIndex(ticket => ticket.TokenHash).IsUnique();
        builder.Property(ticket => ticket.TokenHash).HasMaxLength(128);
    }
}
