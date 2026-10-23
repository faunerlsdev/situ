using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Geography.ListCountries;

namespace Situ.Api.Endpoints.Geography;

public sealed class ListCountriesEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/geography/countries", Handle)
            .WithName("ListCountries")
            .WithTags("Geography")
            .Produces<ListCountriesResponse>()
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        IHandler<ListCountriesRequest, Result<ListCountriesResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListCountriesRequest(), cancellationToken);
        return result.ToHttpResult();
    }
}
