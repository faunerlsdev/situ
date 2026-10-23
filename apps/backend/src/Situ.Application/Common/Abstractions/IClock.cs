namespace Situ.Application.Common.Abstractions;

/// <summary>Application clock. Reads time from <see cref="TimeProvider"/> so handlers never call <c>DateTimeOffset.UtcNow</c>.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
