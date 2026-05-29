namespace MoneyTracker.Domain.Common;

public static class ErrorCodes
{
    // Auth
    public const string EmailTaken = "EMAIL_TAKEN";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";

    // Wallet
    public const string InvalidCreditLimit = "INVALID_CREDIT_LIMIT";
    public const string IdAlreadyExists = "ID_ALREADY_EXISTS";

    // Category
    public const string ParentNotFound = "PARENT_NOT_FOUND";
    public const string ParentTypeMismatch = "PARENT_TYPE_MISMATCH";
    public const string CannotBeOwnParent = "CANNOT_BE_OWN_PARENT";
    public const string HasChildren = "HAS_CHILDREN";
    public const string HasTransactions = "HAS_TRANSACTIONS";
    public const string CategoryAppliesToAll = "CATEGORY_APPLIES_TO_ALL";

    // Generic
    public const string InternalError = "INTERNAL_ERROR";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";

    // Validation field-level
    public const string Required = "REQUIRED";
    public const string InvalidEmail = "INVALID_EMAIL";
    public const string TooShort = "TOO_SHORT";
    public const string TooLong = "TOO_LONG";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string OutOfRange = "OUT_OF_RANGE";
}
