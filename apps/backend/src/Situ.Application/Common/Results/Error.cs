namespace Situ.Application.Common.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Locked,
    RateLimited,
    Failure,
}

public sealed record Error
{
    public Error(
        string code,
        string message,
        ErrorType type,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Error code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Error message is required.", nameof(message));
        }

        Code = code.Trim();
        Message = message.Trim();
        Type = type;
        Extensions = extensions ?? new Dictionary<string, object?>();
    }

    public string Code { get; }

    public string Message { get; }

    public ErrorType Type { get; }

    public IReadOnlyDictionary<string, object?> Extensions { get; }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Locked(string code, string message, DateTimeOffset? retryAfter = null) =>
        new(
            code,
            message,
            ErrorType.Locked,
            retryAfter is { } moment
                ? new Dictionary<string, object?> { ["retryAfter"] = moment }
                : null);

    public static Error RateLimited(string code, string message, DateTimeOffset retryAfter) =>
        new(code, message, ErrorType.RateLimited, new Dictionary<string, object?> { ["retryAfter"] = retryAfter });

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            throw new ArgumentException("At least one validation message is required.", nameof(messages));
        }

        return new("validation.failed", string.Join(" ", messages), ErrorType.Validation);
    }
}
