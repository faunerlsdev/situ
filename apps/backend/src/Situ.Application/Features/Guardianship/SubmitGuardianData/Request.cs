using Situ.Domain.Guardianship;

namespace Situ.Application.Features.Guardianship.SubmitGuardianData;

public sealed record SubmitGuardianDataRequest(
    string Token,
    string FullName,
    GuardianDocumentType DocumentType,
    string DocumentNumber,
    string ContactEmail);
