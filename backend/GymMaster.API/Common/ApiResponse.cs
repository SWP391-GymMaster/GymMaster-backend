namespace GymMaster.API.Common;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiError? Error,
    object? Meta)
{
    public static ApiResponse<T> Ok(T data, object? meta = null)
    {
        return new ApiResponse<T>(true, data, null, meta);
    }

    public static ApiResponse<T> Fail(string code, string message, string requestId)
    {
        return new ApiResponse<T>(false, default, new ApiError(code, message, requestId), null);
    }
}

public sealed record ApiError(string Code, string Message, string RequestId);
