using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Moderation;

namespace Situ.Application.Features.Administration.Takedowns;

public sealed record SubmitTakedownRequest(
    string RequesterName,
    string RequesterEmail,
    string TargetType,
    Guid TargetId,
    string Description,
    string? Website,
    int? ElapsedMs,
    string? IpAddress,
    string? CaptchaToken);

public sealed record SubmitTakedownResponse(bool Accepted, string TrackingCode);

public sealed class SubmitTakedownValidator : AbstractValidator<SubmitTakedownRequest>
{
    public SubmitTakedownValidator()
    {
        RuleFor(request => request.RequesterName).NotEmpty().MaximumLength(120);
        RuleFor(request => request.RequesterEmail).NotEmpty().MaximumLength(254);
        RuleFor(request => request.TargetType).NotEmpty();
        RuleFor(request => request.TargetId).NotEmpty();
        RuleFor(request => request.Description).NotEmpty().MaximumLength(2000);
    }
}

public sealed class SubmitTakedownHandler(IApplicationDbContext db, IClock clock, IEmailSender email, ICaptchaVerifier captcha)
    : IHandler<SubmitTakedownRequest, Result<SubmitTakedownResponse>>
{
    public async Task<Result<SubmitTakedownResponse>> Handle(SubmitTakedownRequest request, CancellationToken cancellationToken)
    {
        var human = false;
        try
        {
            human = await captcha.VerifyAsync(request.CaptchaToken, request.IpAddress, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            human = false;
        }

        if (!human)
        {
            return Result<SubmitTakedownResponse>.Failure(Error.Validation("captcha.invalid", "No pudimos comprobar que eres una persona. Inténtalo de nuevo."));
        }

        if (!Enum.TryParse<ContentTargetType>(request.TargetType, out var targetType))
        {
            return Result<SubmitTakedownResponse>.Failure(Error.Validation("takedown.invalid_target", "Indica qué contenido quieres retirar."));
        }

        if (!Email.TryCreate(request.RequesterEmail, out var address))
        {
            return Result<SubmitTakedownResponse>.Failure(Error.Validation("takedown.invalid_email", "El correo no es válido."));
        }

        var now = clock.UtcNow;
        var since = now.AddHours(-1);
        var recent = await db.TakedownRequests.CountAsync(item => item.RequesterEmail == address.Value && item.CreatedAt >= since, cancellationToken);
        if (recent >= 3)
        {
            return Result<SubmitTakedownResponse>.Failure(Error.RateLimited("takedown.rate_limited", "Ya recibimos varias solicitudes desde ese correo. Espera un momento.", now.AddHours(1)));
        }

        TakedownRequest taken;
        try
        {
            taken = TakedownRequest.Receive(request.RequesterName, address.Value, targetType, request.TargetId, request.Description, now);
        }
        catch (DomainException exception)
        {
            return Result<SubmitTakedownResponse>.Failure(Error.Validation(exception.Code, "Revisa los datos de la solicitud."));
        }

        db.TakedownRequests.Add(taken);
        await db.SaveChangesAsync(cancellationToken);
        var code = Tracking(taken.Id.ToString("N"));
        await TrySendAsync(address.Value, "Recibimos tu solicitud de retiro", $"El número de seguimiento es {code}. Responderemos antes del {taken.DueAt:yyyy-MM-dd}.", cancellationToken);
        return Result<SubmitTakedownResponse>.Success(new SubmitTakedownResponse(true, code));
    }

    private async Task TrySendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            await email.SendAsync(to, subject, body, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The request is already stored. A mail failure does not roll it back.
        }
    }

    internal static string Tracking(string seed) => "TD-" + seed[..8].ToUpperInvariant();
}

public sealed record ListTakedownsRequest(Guid ActorId, string? Status, bool? Overdue, string? Cursor, int Limit);

public sealed record TakedownListItem(Guid Id, string TrackingCode, string Status, DateTimeOffset DueAt, int DaysRemaining, bool Overdue, string TargetType, Guid TargetId);

public sealed record ListTakedownsResponse(IReadOnlyList<TakedownListItem> Items, string? NextCursor);

public sealed class ListTakedownsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListTakedownsRequest, Result<ListTakedownsResponse>>
{
    public async Task<Result<ListTakedownsResponse>> Handle(ListTakedownsRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.TakedownsHandle, cancellationToken) is { } denied)
        {
            return Result<ListTakedownsResponse>.Failure(denied);
        }

        var now = clock.UtcNow;
        var query = db.TakedownRequests.AsQueryable();
        if (Enum.TryParse<TakedownStatus>(request.Status, out var status))
        {
            query = query.Where(item => item.Status == status);
        }

        if (request.Overdue == true)
        {
            query = query.Where(item => (item.Status == TakedownStatus.Received || item.Status == TakedownStatus.UnderReview) && item.DueAt < now);
        }

        if (AdminSupport.TryDecodeCursor(request.Cursor, out var at, out var id))
        {
            query = query.Where(item => item.CreatedAt < at || (item.CreatedAt == at && item.Id.CompareTo(id) < 0));
        }

        var limit = request.Limit is > 0 and <= 50 ? request.Limit : 20;
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = rows.Take(limit).Select(item => new TakedownListItem(
            item.Id,
            SubmitTakedownHandler.Tracking(item.Id.ToString("N")),
            item.Status.ToString(),
            item.DueAt,
            (int)Math.Ceiling((item.DueAt - now).TotalDays),
            item.IsOverdue(now),
            item.TargetType.ToString(),
            item.TargetId)).ToList();
        var next = rows.Count > limit ? AdminSupport.EncodeCursor(rows[limit - 1].CreatedAt, rows[limit - 1].Id) : null;
        return Result<ListTakedownsResponse>.Success(new ListTakedownsResponse(page, next));
    }
}

public sealed record ReviewTakedownRequest(Guid ActorId, string? IpAddress, Guid TakedownId, string Action, string? Resolution);

public sealed class ReviewTakedownValidator : AbstractValidator<ReviewTakedownRequest>
{
    public ReviewTakedownValidator()
    {
        RuleFor(request => request.Action).NotEmpty();
        RuleFor(request => request.Resolution).NotEmpty().MaximumLength(2000).When(request => request.Action is "accept" or "reject");
    }
}

public sealed class ReviewTakedownHandler(IApplicationDbContext db, IClock clock, IEmailSender email)
    : IHandler<ReviewTakedownRequest, Result>
{
    public async Task<Result> Handle(ReviewTakedownRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.TakedownsHandle, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        var taken = await db.TakedownRequests.FirstOrDefaultAsync(item => item.Id == request.TakedownId, cancellationToken);
        if (taken is null)
        {
            return Result.Failure(Error.NotFound("takedown.not_found", "No encontramos la solicitud."));
        }

        var now = clock.UtcNow;
        var before = taken.Status.ToString();
        try
        {
            switch (request.Action)
            {
                case "start-review":
                    taken.StartReview(request.ActorId, now);
                    break;
                case "accept":
                    taken.Accept(request.Resolution!.Trim(), now);
                    await RemoveContentAsync(request.ActorId, taken, request.Resolution!.Trim(), now, cancellationToken);
                    break;
                case "reject":
                    taken.Reject(request.Resolution!.Trim(), now);
                    break;
                default:
                    return Result.Failure(Error.Validation("takedown.unknown_action", "Esa acción no existe."));
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Conflict(exception.Code, "La solicitud no está en el estado necesario."));
        }

        AdminSupport.Record(db, request.ActorId, "takedown." + request.Action, nameof(TakedownRequest), taken.Id.ToString(), AdminSupport.Json(new { before, after = taken.Status.ToString() }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        if (request.Action is "accept" or "reject")
        {
            var subject = request.Action == "accept" ? "Aceptamos tu solicitud de retiro" : "No aceptamos tu solicitud de retiro";
            try
            {
                await email.SendAsync(taken.RequesterEmail, subject, request.Resolution ?? "", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // The decision is already stored.
            }
        }

        return Result.Success();
    }

    private async Task RemoveContentAsync(Guid actorId, TakedownRequest taken, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        db.ModerationActions.Add(ModerationAction.Record(actorId, ModerationActionType.RemoveContent, taken.TargetType, taken.TargetId, null, reason, now));
        if (taken.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == taken.TargetId, cancellationToken);
            report?.Remove(now);
        }
        else if (taken.TargetType == ContentTargetType.Comment)
        {
            var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == taken.TargetId, cancellationToken);
            comment?.Remove(now);
        }
        else if (taken.TargetType == ContentTargetType.LiveStream)
        {
            var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.Id == taken.TargetId, cancellationToken);
            if (stream is null)
            {
                return;
            }

            if (stream.IsLive)
            {
                stream.Cut(reason.Length > 300 ? reason[..300] : reason, null, now);
            }
            else
            {
                stream.Remove(now);
            }
        }
    }
}
