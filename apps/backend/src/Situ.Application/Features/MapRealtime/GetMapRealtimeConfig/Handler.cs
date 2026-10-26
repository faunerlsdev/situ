using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Reports;

namespace Situ.Application.Features.MapRealtime.GetMapRealtimeConfig;

public sealed record GetMapRealtimeConfigRequest;

public sealed record MapRealtimeView(
    double CellDegrees,
    int MaxCells,
    int CoalesceIntervalMilliseconds,
    int SubscribeCallsPerMinute);

public sealed class GetMapRealtimeConfigHandler(IApplicationDbContext db)
    : IHandler<GetMapRealtimeConfigRequest, Result<MapRealtimeView>>
{
    public async Task<Result<MapRealtimeView>> Handle(GetMapRealtimeConfigRequest request, CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        return Result<MapRealtimeView>.Success(Read(settings));
    }

    public static MapRealtimeView Read(IReadOnlyDictionary<string, string> settings) =>
        new(
            MapRealtimeLimits.Degrees(settings.GetValueOrDefault(SystemSettingKeys.MapCellDegrees)),
            MapRealtimeLimits.MaxCells(settings.GetValueOrDefault(SystemSettingKeys.MapMaxCells)),
            MapRealtimeLimits.CoalesceMilliseconds(settings.GetValueOrDefault(SystemSettingKeys.MapCoalesceIntervalMilliseconds)),
            MapRealtimeLimits.SubscribeCallsPerMinute(settings.GetValueOrDefault(SystemSettingKeys.MapSubscribeCallsPerMinute)));
}
