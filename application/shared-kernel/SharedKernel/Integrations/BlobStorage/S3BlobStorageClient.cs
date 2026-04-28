using Amazon.S3;
using Amazon.S3.Model;

namespace SharedKernel.Integrations.BlobStorage;

/// <summary>
/// S3-compatible blob storage client. Works with Scaleway Object Storage, SeaweedFS, MinIO, and AWS S3.
/// </summary>
public sealed class S3BlobStorageClient(IAmazonS3 s3Client, string endpoint, TimeProvider timeProvider) : IBlobStorageClient
{
    public async Task UploadAsync(string containerName, string blobName, string contentType, Stream stream, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = containerName,
            Key = blobName,
            ContentType = contentType,
            InputStream = stream
        };

        await s3Client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<(Stream Stream, string ContentType)?> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3Client.GetObjectAsync(containerName, blobName, cancellationToken);
            return (response.ResponseStream, response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public string GetBlobUrl(string container, string blobName)
    {
        return $"{endpoint.TrimEnd('/')}/{container}/{blobName}";
    }

    public async Task<bool> DeleteIfExistsAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await s3Client.DeleteObjectAsync(containerName, blobName, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public string GetSharedAccessSignature(string container, TimeSpan expiresIn)
    {
        var expiresOn = timeProvider.GetUtcNow().Add(expiresIn);
        var presignedUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = container,
            Expires = expiresOn.UtcDateTime,
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTPS
        });

        // Return just the query string portion (like Azure SAS)
        var uri = new Uri(presignedUrl);
        return uri.Query;
    }

    public Uri GetBlobUriWithSharedAccessSignature(string container, string blobName, TimeSpan expiresIn)
    {
        var expiresOn = timeProvider.GetUtcNow().Add(expiresIn);
        var presignedUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = container,
            Key = blobName,
            Expires = expiresOn.UtcDateTime,
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTPS
        });

        return new Uri(presignedUrl);
    }

    public async Task CreateContainerIfNotExistsAsync(string containerName, BlobPublicAccessType publicAccessType, CancellationToken cancellationToken)
    {
        try
        {
            await s3Client.PutBucketAsync(containerName, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyExists" or "BucketAlreadyOwnedByYou")
        {
            // Bucket already exists
        }
    }
}
