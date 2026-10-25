using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ListMyDevices;

public sealed record ListMyDevicesRequest;

public sealed record ListMyDevicesResponse(IReadOnlyList<DeviceItem> Devices);

public sealed record DeviceItem(
    Guid Id,
    string DeviceIdentifier,
    DevicePlatform Platform,
    string? DeviceName,
    DateTimeOffset LastSeenAt,
    bool IsActive,
    bool IsCurrent);
