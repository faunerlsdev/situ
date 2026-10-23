using Situ.Domain.Legal;

namespace Situ.Application.Features.Legal.ListLegalDocuments;

public sealed record ListLegalDocumentsResponse(IReadOnlyList<LegalDocumentItem> Documents);

public sealed record LegalDocumentItem(
    Guid Id,
    LegalDocumentType Type,
    string Version,
    string Url,
    string Locale);
