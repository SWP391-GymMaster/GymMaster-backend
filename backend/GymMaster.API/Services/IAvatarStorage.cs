namespace GymMaster.API.Services;

public interface IAvatarStorage
{
    Task<string> UploadAsync(
        long userId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}
