namespace Acman.BuildingBlocks.Errors;

public class AppException : Exception
{
    public AppException(string message, string code = "app_error", int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message, string code = "not_found")
        : base(message, code, 404)
    {
    }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message, string code = "forbidden")
        : base(message, code, 403)
    {
    }
}

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(string message, string code = "validation_error")
        : base(message, code, 400)
    {
    }
}
