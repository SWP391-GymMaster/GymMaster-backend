namespace GymMaster.API.Services;

public sealed class AvatarStorageException : Exception
{
    public AvatarStorageException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
