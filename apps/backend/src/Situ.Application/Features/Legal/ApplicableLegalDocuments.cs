using Situ.Domain.Legal;

namespace Situ.Application.Features.Legal;

/// <summary>
/// The legal texts a person must accept for a country: active documents for that country,
/// falling back to a global document when the country has none of that type.
/// </summary>
public static class ApplicableLegalDocuments
{
    public static IReadOnlyList<LegalDocument> SelectCurrent(IEnumerable<LegalDocument> documents, Guid countryId) =>
        documents
            .Where(document => document.IsActive && (document.CountryId is null || document.CountryId == countryId))
            .GroupBy(document => document.Type)
            .Select(group => group
                .OrderByDescending(document => document.CountryId == countryId)
                .ThenByDescending(document => document.PublishedAt)
                .First())
            .OrderBy(document => document.Type)
            .ToArray();
}
