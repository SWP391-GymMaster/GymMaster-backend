namespace GymMaster.API.Common;
public sealed record ServiceResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    string? ErrorMessage,
    int StatusCode)
{
    public static ServiceResult<T> Success(T value, int statusCode = StatusCodes.Status200OK)
    {
        return new ServiceResult<T>(true, value, null, null, statusCode);
    }

    public static ServiceResult<T> Failure(string code, string message, int statusCode)
    {
        return new ServiceResult<T>(false, default, code, message, statusCode);
    }
}
