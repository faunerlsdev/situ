using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Situ.Infrastructure.Persistence.Configurations;

internal static class EntityBuilderExtensions
{
    public static void Required<TDependent, TPrincipal>(
        this EntityTypeBuilder<TDependent> builder,
        Expression<Func<TDependent, object?>> foreignKey)
        where TDependent : class
        where TPrincipal : class
    {
        builder.HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(DeleteBehavior.Restrict);
    }

    public static void Optional<TDependent, TPrincipal>(
        this EntityTypeBuilder<TDependent> builder,
        Expression<Func<TDependent, object?>> foreignKey)
        where TDependent : class
        where TPrincipal : class
    {
        builder.HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(DeleteBehavior.Restrict);
    }
}
