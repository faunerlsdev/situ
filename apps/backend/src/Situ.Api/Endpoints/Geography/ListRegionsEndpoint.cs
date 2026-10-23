using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Geography.ListRegions;

namespace Situ.Api.Endpoints.Geography;

public sealed class ListRegionsEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/geography/countries/{countryId:guid}/regions", Handle)
            .WithName("ListRegions")
            .WithTags("Geography")
            .Produces<ListRegionsResponse>()
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        Guid countryId,
        IHandler<ListRegionsRequest, Result<ListRegionsResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListRegionsRequest(countryId), cancellationToken);
        return result.ToHttpResult();
    }
}
