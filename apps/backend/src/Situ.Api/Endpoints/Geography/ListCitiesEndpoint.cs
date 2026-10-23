using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Geography.ListCities;

namespace Situ.Api.Endpoints.Geography;

public sealed class ListCitiesEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/geography/regions/{regionId:guid}/cities", Handle)
            .WithName("ListCities")
            .WithTags("Geography")
            .Produces<ListCitiesResponse>()
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        Guid regionId,
        IHandler<ListCitiesRequest, Result<ListCitiesResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListCitiesRequest(regionId), cancellationToken);
        return result.ToHttpResult();
    }
}
