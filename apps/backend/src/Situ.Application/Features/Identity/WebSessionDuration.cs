using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity;

internal static class WebSessionDuration
{
    public static async Task<TimeSpan> ForDeviceAsync(IApplicationDbContext db, UserDevice device, CancellationToken cancellationToken)
    {
        if (device.Platform != DevicePlatform.Web)
        {
            return SessionPolicy.RefreshTokenLifetime;
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var days = device.RememberWebSession
            ? ReportSettings.Int(settings, SystemSettingKeys.WebSessionRememberDays, 90)
            : ReportSettings.Int(settings, SystemSettingKeys.WebSessionRefreshDays, 30);
        return TimeSpan.FromDays(Math.Clamp(days, 1, 365));
    }
}
