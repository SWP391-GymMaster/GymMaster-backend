namespace GymMaster.API.Services;

public sealed record AuthServiceResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage,
    int StatusCode)
{
    public static AuthServiceResult<T> Success(T value, int statusCode = StatusCodes.Status200OK)
    {
        return new AuthServiceResult<T>(true, value, null, null, statusCode);
    }

    public static AuthServiceResult<T> Failure(string code, string message, int statusCode)
    {
        return new AuthServiceResult<T>(false, default, code, message, statusCode);
    }
}
