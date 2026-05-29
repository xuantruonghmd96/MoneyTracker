namespace MoneyTracker.Api.Services.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }
    public Dictionary<string, string>? Fields { get; }

    public DomainException(string errorCode, string? message = null, Dictionary<string, string>? fields = null)
        : base(message ?? errorCode)
    {
        ErrorCode = errorCode;
        Fields = fields;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string errorCode = "NOT_FOUND") : base(errorCode) { }
}

public class ConflictException : DomainException
{
    public ConflictException(string errorCode) : base(errorCode) { }
}

public class ValidationException : DomainException
{
    public ValidationException(string errorCode, Dictionary<string, string>? fields = null)
        : base(errorCode, null, fields) { }
}

public class ForbiddenException : DomainException
{
    public ForbiddenException(string errorCode) : base(errorCode) { }
}
