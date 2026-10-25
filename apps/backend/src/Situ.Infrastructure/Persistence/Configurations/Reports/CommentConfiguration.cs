using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Situ.Domain.Identity;
using Situ.Domain.Reports;
using Situ.Infrastructure.Persistence.Configurations;

namespace Situ.Infrastructure.Persistence.Configurations.Reports;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.Property(e => e.Text).HasMaxLength(500);
        builder.Required<Comment, User>(e => e.AuthorId);
        builder.Optional<Comment, Comment>(e => e.ParentCommentId);
        builder.HasIndex(e => new { e.TargetType, e.TargetId, e.CreatedAt });
        builder.HasIndex(e => new { e.ParentCommentId, e.CreatedAt });
    }
}
