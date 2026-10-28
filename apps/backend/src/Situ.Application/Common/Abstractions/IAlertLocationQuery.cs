using Situ.Domain.Common;

namespace Situ.Application.Common.Abstractions;

public sealed record ProximityMatch(Guid UserId, double DistanceMeters);

/// <summary>Finds devices whose alert location is inside that user's own radius. Implementations must not log coordinates.</summary>
public interface IAlertLocationQuery
{
    Task<IReadOnlyList<ProximityMatch>> MatchAsync(GeoPoint origin, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Devices whose last alert location is still inside the TTL and within <paramref name="radiusMeters"/> of the origin. Does not use each person's own radius.</summary>
    Task<IReadOnlyList<ProximityMatch>> MatchWithinAsync(GeoPoint origin, int radiusMeters, DateTimeOffset now, CancellationToken cancellationToken);
}
