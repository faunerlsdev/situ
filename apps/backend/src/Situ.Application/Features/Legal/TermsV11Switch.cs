using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Legal;

namespace Situ.Application.Features.Legal;

/// <summary>
/// Turns terms 1.1 into the version registration records, and puts 2026-10-29 back when reverted.
/// The public page is a separate file change. This class only updates the legal_documents rows.
/// </summary>
public static class TermsV11Switch
{
    public const string PreviousVersion = "2026-10-29";
    public const string NextVersion = "1.1";

    public static Task<Result> ActivateAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        ApplyAsync(db, activateNext: true, cancellationToken);

    public static Task<Result> RevertAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        ApplyAsync(db, activateNext: false, cancellationToken);

    private static async Task<Result> ApplyAsync(IApplicationDbContext db, bool activateNext, CancellationToken cancellationToken)
    {
        var documents = await db.LegalDocuments
            .Where(item => item.Type == LegalDocumentType.TermsOfUse && (item.Version == PreviousVersion || item.Version == NextVersion))
            .ToListAsync(cancellationToken);
        var previous = documents.Where(item => item.Version == PreviousVersion).ToList();
        var next = documents.Where(item => item.Version == NextVersion).ToList();
        if (previous.Count == 0 || next.Count == 0)
        {
            return Result.Failure(Error.NotFound(
                "terms.v11_missing",
                "Falta la fila de términos 2026-10-29 o la 1.1. Aplica antes la migración 20261030210000_InactiveTermsV11."));
        }

        foreach (var document in activateNext ? previous : next)
        {
            document.Retire();
        }

        foreach (var document in activateNext ? next : previous)
        {
            document.Activate();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
