namespace Situ.Application.Common.Abstractions;

/// <summary>
/// Pushes a zone batch to whoever is subscribed to that cell.
/// The payload is the signal list and nothing else.
/// </summary>
public interface IMapZonePublisher
{
    Task PublishAsync(string cellId, IReadOnlyList<MapZoneSignal> events, CancellationToken cancellationToken);
}

public sealed record MapZoneSignal(string Type, Guid ReportId, string CellId, DateTimeOffset OccurredAt);
