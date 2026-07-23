using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Qaysar.Api.Configuration;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

/// <summary>
/// Cloudflare R2 storage service. R2 is S3-compatible; we use AWS SDK
/// with a custom ServiceURL. Credentials come from env vars — add them
/// later, the service will throw a clear error when actually invoked.
/// </summary>
public class R2StorageService : IStorageService
{
    private readonly R2Options _opts;

    public R2StorageService(IOptions<R2Options> opts)
    {
        _opts = opts.Value;
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(_opts.AccountId) ||
            string.IsNullOrWhiteSpace(_opts.AccessKey) ||
            string.IsNullOrWhiteSpace(_opts.SecretKey) ||
            string.IsNullOrWhiteSpace(_opts.Bucket))
        {
            throw new InvalidOperationException(
                "R2 storage is not configured. Set R2_ACCOUNT_ID, R2_ACCESS_KEY, R2_SECRET_KEY, R2_BUCKET in .env");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{_opts.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
        };
        using var client = new AmazonS3Client(_opts.AccessKey, _opts.SecretKey, config);

        var ext = Path.GetExtension(fileName);
        var key = $"products/{Guid.NewGuid():N}{ext}";

        var req = new PutObjectRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            DisablePayloadSigning = true,
        };
        await client.PutObjectAsync(req);

        var baseUrl = string.IsNullOrWhiteSpace(_opts.PublicBaseUrl)
            ? $"https://{_opts.Bucket}.{_opts.AccountId}.r2.cloudflarestorage.com"
            : _opts.PublicBaseUrl.TrimEnd('/');

        return $"{baseUrl}/{key}";
    }
}
