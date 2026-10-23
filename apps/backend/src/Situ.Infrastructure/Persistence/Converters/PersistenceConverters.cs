using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using Situ.Domain.Common;

namespace Situ.Infrastructure.Persistence.Converters;

public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value)
    {
    }
}

public sealed class NullableUtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, DateTimeOffset?>
{
    public NullableUtcDateTimeOffsetConverter()
        : base(value => value.HasValue ? value.Value.ToUniversalTime() : null, value => value)
    {
    }
}

public sealed class EmailConverter : ValueConverter<Email, string>
{
    public EmailConverter()
        : base(email => email.Value, value => Email.Create(value))
    {
    }
}

public sealed class EmailComparer : ValueComparer<Email>
{
    public EmailComparer()
        : base(
            (left, right) => left!.Value == right!.Value,
            value => value.Value.GetHashCode(StringComparison.Ordinal),
            value => Email.Create(value.Value))
    {
    }
}

public sealed class UserNameConverter : ValueConverter<UserName, string>
{
    public UserNameConverter()
        : base(userName => userName.Value, value => UserName.Create(value))
    {
    }
}

public sealed class UserNameComparer : ValueComparer<UserName>
{
    public UserNameComparer()
        : base(
            (left, right) => left!.Value == right!.Value,
            value => value.Value.GetHashCode(StringComparison.Ordinal),
            value => UserName.Create(value.Value))
    {
    }
}

public sealed class GeoPointConverter : ValueConverter<GeoPoint, Point>
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public GeoPointConverter()
        : base(point => ToPoint(point), point => FromPoint(point))
    {
    }

    public static Point ToPoint(GeoPoint point) => Factory.CreatePoint(new Coordinate(point.Longitude, point.Latitude));

    public static GeoPoint FromPoint(Point point) => new(point.Y, point.X);
}

public sealed class GeoPointComparer : ValueComparer<GeoPoint>
{
    public GeoPointComparer()
        : base(
            (left, right) => left.Latitude == right.Latitude && left.Longitude == right.Longitude,
            value => HashCode.Combine(value.Latitude, value.Longitude),
            value => value)
    {
    }
}

public sealed class NullableGeoPointConverter : ValueConverter<GeoPoint?, Point?>
{
    public NullableGeoPointConverter()
        : base(
            point => point.HasValue ? GeoPointConverter.ToPoint(point.Value) : null,
            point => point == null ? null : GeoPointConverter.FromPoint(point))
    {
    }
}

public sealed class NullableGeoPointComparer : ValueComparer<GeoPoint?>
{
    public NullableGeoPointComparer()
        : base(
            (left, right) =>
                left.HasValue == right.HasValue
                && (!left.HasValue
                    || (left.Value.Latitude == right!.Value.Latitude && left.Value.Longitude == right.Value.Longitude)),
            value => value.HasValue ? HashCode.Combine(value.Value.Latitude, value.Value.Longitude) : 0,
            value => value)
    {
    }
}
