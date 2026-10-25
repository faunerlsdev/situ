using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;

namespace Situ.Application.Features.Configuration.GetAppConfig;

public sealed record GetAppConfigRequest;

public sealed record PanelCopyView(string Browse, string Report);

public sealed record MediaLimitsView(int MaxImages, int MaxVideos, int MaxVideoSeconds, long MaxImageBytes, long MaxVideoBytes);

public sealed record PlatformVersionView(string MinSupported, string Latest);

public sealed record AppVersionsView(PlatformVersionView Ios, PlatformVersionView Android, PlatformVersionView Web);

public sealed record LegalLinksView(string TermsUrl, string PrivacyPolicyUrl, string SupportUrl);

public sealed record GetAppConfigResponse(
    PanelCopyView PanelCopy,
    MediaLimitsView MediaLimits,
    int ReportDescriptionMaxLength,
    int ConfirmationMaxDistanceMeters,
    string AnonymityDisplayName,
    AppVersionsView Versions,
    LegalLinksView Legal);

public sealed class GetAppConfigHandler(IApplicationDbContext db)
    : IHandler<GetAppConfigRequest, Result<GetAppConfigResponse>>
{
    public async Task<Result<GetAppConfigResponse>> Handle(GetAppConfigRequest request, CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        return Result<GetAppConfigResponse>.Success(new GetAppConfigResponse(
            new PanelCopyView(
                Text(settings, SystemSettingKeys.PanelBrowseCopy, "Quiero chismosear"),
                Text(settings, SystemSettingKeys.PanelReportCopy, "Quiero reportar algo")),
            new MediaLimitsView(
                ReportSettings.Int(settings, SystemSettingKeys.ReportMaxImages, 5),
                ReportSettings.Int(settings, SystemSettingKeys.ReportMaxVideos, 1),
                ReportSettings.Int(settings, SystemSettingKeys.ReportMaxVideoSeconds, 60),
                ReportSettings.Long(settings, SystemSettingKeys.ReportMaxImageBytes, 10 * 1024 * 1024),
                ReportSettings.Long(settings, SystemSettingKeys.ReportMaxVideoBytes, 100 * 1024 * 1024)),
            ReportSettings.Int(settings, SystemSettingKeys.ReportDescriptionMaxLength, 1000),
            ReportSettings.Int(settings, SystemSettingKeys.ReportConfirmationRadiusMeters, 2000),
            ReportSettings.DisplayName(settings),
            new AppVersionsView(
                Version(settings, SystemSettingKeys.AppIosMinSupportedVersion, SystemSettingKeys.AppIosLatestVersion),
                Version(settings, SystemSettingKeys.AppAndroidMinSupportedVersion, SystemSettingKeys.AppAndroidLatestVersion),
                Version(settings, SystemSettingKeys.AppWebMinSupportedVersion, SystemSettingKeys.AppWebLatestVersion)),
            new LegalLinksView(
                Text(settings, SystemSettingKeys.LegalTermsUrl, "https://somossitu.com/terminos"),
                Text(settings, SystemSettingKeys.LegalPrivacyUrl, "https://somossitu.com/privacidad"),
                Text(settings, SystemSettingKeys.LegalSupportUrl, "https://somossitu.com/soporte"))));
    }

    private static string Text(IReadOnlyDictionary<string, string> settings, string key, string fallback)
    {
        var value = settings.GetValueOrDefault(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static PlatformVersionView Version(IReadOnlyDictionary<string, string> settings, string minKey, string latestKey) =>
        new(Text(settings, minKey, "0.1.0"), Text(settings, latestKey, "0.1.0"));
}
