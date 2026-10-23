using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Legal.ListLegalDocuments;

namespace Situ.Api.Endpoints.Legal;

public sealed class ListLegalDocumentsEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/legal/documents", Handle)
            .WithName("ListLegalDocuments")
            .WithTags("Legal")
            .Produces<ListLegalDocumentsResponse>()
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        Guid countryId,
        IHandler<ListLegalDocumentsRequest, Result<ListLegalDocumentsResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListLegalDocumentsRequest(countryId), cancellationToken);
        return result.ToHttpResult();
    }
}
