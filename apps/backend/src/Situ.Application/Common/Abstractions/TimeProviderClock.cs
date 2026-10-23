namespace Situ.Application.Common.Abstractions;

public sealed class TimeProviderClock(TimeProvider time) : IClock
{
    public DateTimeOffset UtcNow => time.GetUtcNow();
}
