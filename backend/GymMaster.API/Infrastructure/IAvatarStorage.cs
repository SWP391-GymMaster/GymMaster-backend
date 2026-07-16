namespace GymMaster.API.Infrastructure;
public interface IAvatarStorage
{
    Task<string> UploadAsync(
        long userId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}
