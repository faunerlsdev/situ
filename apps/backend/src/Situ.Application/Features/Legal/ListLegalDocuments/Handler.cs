using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Legal.ListLegalDocuments;

public sealed class ListLegalDocumentsHandler(IApplicationDbContext db)
    : IHandler<ListLegalDocumentsRequest, Result<ListLegalDocumentsResponse>>
{
    public async Task<Result<ListLegalDocumentsResponse>> Handle(
        ListLegalDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        var countryEnabled = await db.Countries
            .AnyAsync(country => country.Id == request.CountryId && country.IsEnabled, cancellationToken);
        if (!countryEnabled)
        {
            return Result<ListLegalDocumentsResponse>.Failure(
                Error.NotFound("geography.country_not_found", "Ese país no está disponible."));
        }

        var published = await db.LegalDocuments
            .AsNoTracking()
            .Where(document => document.IsActive)
            .ToListAsync(cancellationToken);

        var documents = ApplicableLegalDocuments.SelectCurrent(published, request.CountryId)
            .Select(document => new LegalDocumentItem(
                document.Id,
                document.Type,
                document.Version,
                document.Url,
                document.Locale))
            .ToArray();

        return Result<ListLegalDocumentsResponse>.Success(new ListLegalDocumentsResponse(documents));
    }
}
