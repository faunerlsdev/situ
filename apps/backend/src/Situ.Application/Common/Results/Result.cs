namespace Situ.Application.Common.Results;

public class Result : IResultFailure<Result>
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess == error is not null)
        {
            throw new ArgumentException("A success result cannot carry an error, and a failure requires one.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, error);
    }

    static Result IResultFailure<Result>.Failure(Error error) => Failure(error);
}

public sealed class Result<T> : Result, IResultFailure<Result<T>>
{
    private Result(T value)
        : base(true, null)
    {
        Value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        Value = default;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    static Result<T> IResultFailure<Result<T>>.Failure(Error error) => Failure(error);
}

/// <summary>Lets the validation decorator build a failed <see cref="Result"/> or <see cref="Result{T}"/> without knowing T.</summary>
internal interface IResultFailure<TSelf>
    where TSelf : Result
{
    static abstract TSelf Failure(Error error);
}
