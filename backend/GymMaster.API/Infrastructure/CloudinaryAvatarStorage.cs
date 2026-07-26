using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using GymMaster.API.Options;
using Microsoft.Extensions.Options;

namespace GymMaster.API.Infrastructure;
// Adapter I/O thuan (upload len Cloudinary) — kiem chung bang manual/integration, khong unit test.
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class CloudinaryAvatarStorage : IAvatarStorage
{
    private readonly CloudinaryOptions _options;

    public CloudinaryAvatarStorage(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadAsync(
        long userId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new AvatarStorageException(
                "CLOUDINARY_NOT_CONFIGURED",
                "Cloudinary chua duoc cau hinh.");
        }

        var cloudinary = new Cloudinary(new Account(
            _options.CloudName,
            _options.ApiKey,
            _options.ApiSecret));

        var upload = await cloudinary.UploadAsync(
            new ImageUploadParams
            {
                File = new FileDescription($"user_{userId}", content),
                Folder = "gymmaster/avatars",
                PublicId = $"user_{userId}",
                Overwrite = true,
                Transformation = new Transformation()
                    .Width(256)
                    .Height(256)
                    .Crop("fill")
                    .Gravity("face")
            },
            cancellationToken);

        if (upload.Error is not null)
        {
            throw new AvatarStorageException(
                "AVATAR_UPLOAD_FAILED",
                upload.Error.Message);
        }

        var secureUrl = upload.SecureUrl?.ToString();
        if (string.IsNullOrWhiteSpace(secureUrl))
        {
            throw new AvatarStorageException(
                "AVATAR_UPLOAD_FAILED",
                "Cloudinary khong tra ve URL anh.");
        }

        return secureUrl;
    }
}
