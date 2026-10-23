using FluentValidation;
using FluentValidation.Results;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Behaviors;

/// <summary>Runs every <see cref="IValidator{T}"/> for the request and returns a validation <see cref="Error"/> before the handler.</summary>
internal sealed class ValidationHandlerDecorator<TRequest, TResponse> : IHandler<TRequest, TResponse>
    where TResponse : Result, IResultFailure<TResponse>
{
    private readonly IHandler<TRequest, TResponse> _inner;
    private readonly IReadOnlyList<IValidator<TRequest>> _validators;

    public ValidationHandlerDecorator(
        IHandler<TRequest, TResponse> inner,
        IEnumerable<IValidator<TRequest>> validators)
    {
        _inner = inner;
        _validators = validators.ToArray();
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        if (_validators.Count == 0)
        {
            return await _inner.Handle(request, cancellationToken);
        }

        var messages = new List<string>();
        foreach (var validator in _validators)
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                messages.AddRange(validation.Errors.Select(FormatFailure));
            }
        }

        if (messages.Count > 0)
        {
            return TResponse.Failure(Error.Validation(messages));
        }

        return await _inner.Handle(request, cancellationToken);
    }

    private static string FormatFailure(ValidationFailure failure) =>
        string.IsNullOrWhiteSpace(failure.PropertyName)
            ? failure.ErrorMessage
            : $"{failure.PropertyName}: {failure.ErrorMessage}";
}
