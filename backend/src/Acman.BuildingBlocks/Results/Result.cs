namespace Acman.BuildingBlocks.Results;

public class Result
{
    protected Result(bool isSuccess, string? error, string? errorCode = null)
    {
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException("Successful result cannot have an error.");
        }

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException("Failed result must have an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public string? ErrorCode { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error, string? errorCode = null) => Result<T>.Failure(error, errorCode);
}

public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, null)
    {
        _value = value;
    }

    private Result(string error, string? errorCode) : base(false, error, errorCode)
    {
        _value = default;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(string error, string? errorCode = null) => new(error, errorCode);
}
