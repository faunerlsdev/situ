using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Location;

namespace Situ.Api.Endpoints.Location;

public sealed class LocationEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/me/location", Post)
            .WithName("PostMyLocation")
            .WithTags("Location")
            .Produces<PostLocationResponse>()
            .RequireRateLimiting("location-post")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/me/location/pause", Pause)
            .WithName("PauseMyLocation")
            .WithTags("Location")
            .Produces<PauseLocationResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/me/location-sharing", GetSharing)
            .WithName("GetLocationSharing")
            .WithTags("Location")
            .Produces<LocationSharingView>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPut("/me/location-sharing", SetSharing)
            .WithName("SetLocationSharing")
            .WithTags("Location")
            .Produces<LocationSharingView>()
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/me/location-sharing/reminder", Remind)
            .WithName("AcknowledgeLocationReminder")
            .WithTags("Location")
            .Produces<LocationSharingView>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPut("/me/public-location", SetPublic)
            .WithName("SetPublicLocation")
            .WithTags("Location")
            .Produces<LocationSharingView>()
            .RequireRateLimiting("profile-write")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/me/privacy-zones", Zones)
            .WithName("ListPrivacyZones")
            .WithTags("Location")
            .Produces<ListPrivacyZonesResponse>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPost("/me/privacy-zones", CreateZone)
            .WithName("CreatePrivacyZone")
            .WithTags("Location")
            .Produces<PrivacyZoneView>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapPut("/me/privacy-zones/{id:guid}", UpdateZone)
            .WithName("UpdatePrivacyZone")
            .WithTags("Location")
            .Produces<PrivacyZoneView>()
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapDelete("/me/privacy-zones/{id:guid}", DeleteZone)
            .WithName("DeletePrivacyZone")
            .WithTags("Location")
            .RequireAuthorization(AuthorizationPolicies.User);

        group.MapGet("/map/people", People)
            .WithName("GetMapPeople")
            .WithTags("Location")
            .Produces<GetMapPeopleResponse>()
            .RequireRateLimiting("location-read")
            .RequireAuthorization(AuthorizationPolicies.User);
    }

    private static async Task<IResult> Post(LocationBody body, ICurrentUser current, IHandler<PostLocationRequest, Result<PostLocationResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new PostLocationRequest(userId, body.Latitude, body.Longitude, body.AccuracyMeters, body.CapturedAt), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Pause(ICurrentUser current, IHandler<PauseLocationRequest, Result<PauseLocationResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new PauseLocationRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> GetSharing(ICurrentUser current, IHandler<GetLocationSharingRequest, Result<LocationSharingView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new GetLocationSharingRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> SetSharing(SharingBody body, ICurrentUser current, IHandler<SetLocationSharingRequest, Result<LocationSharingView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SetLocationSharingRequest(userId, body.Enabled, body.Mode, body.Duration, body.FriendIds, body.ConsentVersion, body.Ghost), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Remind(ICurrentUser current, IHandler<AcknowledgeLocationReminderRequest, Result<LocationSharingView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new AcknowledgeLocationReminderRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> SetPublic(PublicLocationBody body, ICurrentUser current, IHandler<SetPublicLocationRequest, Result<LocationSharingView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SetPublicLocationRequest(userId, body.Mode, body.ConsentVersion), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Zones(ICurrentUser current, IHandler<ListPrivacyZonesRequest, Result<ListPrivacyZonesResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new ListPrivacyZonesRequest(userId), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> CreateZone(ZoneBody body, ICurrentUser current, IHandler<SavePrivacyZoneRequest, Result<PrivacyZoneView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SavePrivacyZoneRequest(userId, null, body.Name, body.Latitude, body.Longitude, body.RadiusMeters, body.Mode), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> UpdateZone(Guid id, ZoneBody body, ICurrentUser current, IHandler<SavePrivacyZoneRequest, Result<PrivacyZoneView>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new SavePrivacyZoneRequest(userId, id, body.Name, body.Latitude, body.Longitude, body.RadiusMeters, body.Mode), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> DeleteZone(Guid id, ICurrentUser current, IHandler<DeletePrivacyZoneRequest, Result> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new DeletePrivacyZoneRequest(userId, id), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> People(double minLat, double minLon, double maxLat, double maxLon, ICurrentUser current, IHandler<GetMapPeopleRequest, Result<GetMapPeopleResponse>> handler, CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        return (await handler.Handle(new GetMapPeopleRequest(userId, minLat, minLon, maxLat, maxLon), cancellationToken)).ToHttpResult();
    }

    private sealed record LocationBody(double Latitude, double Longitude, double? AccuracyMeters, DateTimeOffset? CapturedAt);

    private sealed record SharingBody(bool Enabled, string? Mode, string? Duration, Guid[]? FriendIds, string? ConsentVersion, string? Ghost);

    private sealed record PublicLocationBody(string Mode, string? ConsentVersion);

    private sealed record ZoneBody(string Name, double Latitude, double Longitude, int RadiusMeters, string Mode);
}
