using Situ.Domain.Common;

namespace Situ.Application.Common.Abstractions;

public sealed record ProximityMatch(Guid UserId, double DistanceMeters);

/// <summary>Finds devices whose alert location is inside that user's own radius. Implementations must not log coordinates.</summary>
public interface IAlertLocationQuery
{
    Task<IReadOnlyList<ProximityMatch>> MatchAsync(GeoPoint origin, DateTimeOffset now, CancellationToken cancellationToken);
}
