namespace Cafeteria.Application;

public sealed class AppException : Exception
{
    public AppException(int statusCode, string message, string? code = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string? Code { get; }

    public static AppException BadRequest(string message, string? code = null) =>
        new(StatusCodes.Status400BadRequest, message, code);

    public static AppException Unauthorized(string message = "Authentication is required.") =>
        new(StatusCodes.Status401Unauthorized, message, "unauthorized");

    public static AppException Forbidden(string message = "You do not have permission to do that.") =>
        new(StatusCodes.Status403Forbidden, message, "forbidden");

    public static AppException NotFound(string message = "The requested resource was not found.") =>
        new(StatusCodes.Status404NotFound, message, "not_found");

    public static AppException Conflict(string message, string? code = null) =>
        new(StatusCodes.Status409Conflict, message, code);

    public static AppException TooManyRequests(string message = "Too many requests.") =>
        new(StatusCodes.Status429TooManyRequests, message, "rate_limited");
}

internal static class StatusCodes
{
    public const int Status400BadRequest = 400;
    public const int Status401Unauthorized = 401;
    public const int Status403Forbidden = 403;
    public const int Status404NotFound = 404;
    public const int Status409Conflict = 409;
    public const int Status429TooManyRequests = 429;
}
