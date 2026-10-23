using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Situ.Domain.Common;
using Situ.Infrastructure.Persistence.Naming;

namespace Situ.Infrastructure.Persistence.Conventions;

internal static class RelationalModelConventions
{
    public static void IgnoreComputedMembers(ModelBuilder modelBuilder)
    {
        var entityTypes = typeof(Entity).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(Entity).IsAssignableFrom(type));

        foreach (var type in entityTypes)
        {
            var builder = modelBuilder.Entity(type);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var declared = property.DeclaringType?.GetProperty(
                    property.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (declared?.GetSetMethod(nonPublic: true) is null)
                {
                    builder.Ignore(property.Name);
                }
            }
        }
    }

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (entity.ClrType is not { IsClass: true } || entity.IsOwned())
            {
                continue;
            }

            var table = SnakeCase.PluralTable(entity.ClrType.Name);
            entity.SetTableName(table);

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase.FromPascal(property.Name));
                ConfigureEnum(property);

                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("timestamp with time zone");
                }

                if (property.ClrType == typeof(GeoPoint) || property.ClrType == typeof(GeoPoint?))
                {
                    property.SetColumnType("geography(point, 4326)");
                }

                if (property.ClrType == typeof(Guid) && property.IsPrimaryKey())
                {
                    property.ValueGenerated = ValueGenerated.Never;
                }
            }

            var geoProperties = entity.GetProperties()
                .Where(property => property.ClrType == typeof(GeoPoint) || property.ClrType == typeof(GeoPoint?))
                .ToList();

            if (geoProperties.Count > 0)
            {
                var builder = modelBuilder.Entity(entity.ClrType);
                foreach (var property in geoProperties)
                {
                    builder.HasIndex(property.Name).HasMethod("gist");
                }
            }
        }

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.IsOwned())
            {
                continue;
            }

            var table = entity.GetTableName();
            if (string.IsNullOrEmpty(table))
            {
                continue;
            }

            foreach (var key in entity.GetKeys())
            {
                var columns = Columns(key.Properties);
                key.SetName(key.IsPrimaryKey() ? $"pk_{table}" : $"ak_{table}_{columns}");
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName($"fk_{table}_{Columns(foreignKey.Properties)}");
            }

            foreach (var index in entity.GetIndexes())
            {
                var prefix = index.IsUnique ? "ux" : "ix";
                index.SetDatabaseName($"{prefix}_{table}_{Columns(index.Properties)}");
            }
        }
    }

    private static string Columns(IEnumerable<IReadOnlyProperty> properties) =>
        string.Join("_", properties.Select(property => SnakeCase.FromPascal(property.Name)));

    private static void ConfigureEnum(IMutableProperty property)
    {
        var underlying = Nullable.GetUnderlyingType(property.ClrType);
        if (underlying is { IsEnum: true })
        {
            typeof(RelationalModelConventions)
                .GetMethod(nameof(SetNullableEnum), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(underlying)
                .Invoke(null, [property]);
            return;
        }

        if (property.ClrType.IsEnum)
        {
            typeof(RelationalModelConventions)
                .GetMethod(nameof(SetEnum), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(property.ClrType)
                .Invoke(null, [property]);
        }
    }

    private static void SetEnum<TEnum>(IMutableProperty property)
        where TEnum : struct, Enum
    {
        property.SetValueConverter(new ValueConverter<TEnum, string>(value => value.ToString(), value => Enum.Parse<TEnum>(value)));
        property.SetMaxLength(40);
    }

    private static void SetNullableEnum<TEnum>(IMutableProperty property)
        where TEnum : struct, Enum
    {
        property.SetValueConverter(new ValueConverter<TEnum?, string?>(
            value => value.HasValue ? value.Value.ToString() : null,
            value => value == null ? null : Enum.Parse<TEnum>(value)));
        property.SetMaxLength(40);
        property.IsNullable = true;
    }
}
