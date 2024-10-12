using Shared.Interfaces;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace Shared.Services;

public class ObjectStorageService(string awsAccessKeyId, string awsSecretAccessKey, AmazonS3Config clientConfig, string bucketName) : IObjectStorageService
{
  private IAmazonS3 _s3Client = new AmazonS3Client(awsAccessKeyId, awsSecretAccessKey, clientConfig);
  private readonly string _bucketName = bucketName;

  public async Task Delete(string key)
  {
    try
    {
      var deleteObjectRequest = new DeleteObjectRequest
      {
        BucketName = _bucketName,
        Key = key
      };

      await _s3Client.DeleteObjectAsync(deleteObjectRequest);
    }
    catch (AmazonS3Exception e)
    {
      Console.WriteLine($"Error deleting file from {_bucketName}: {e.Message}");
    }
  }

  public async Task<Stream?> Get(string key)
  {
    try
    {
      await EnsureBucketExistsAsync();
      var getObjectRequest = new GetObjectRequest
      {
        BucketName = _bucketName,
        Key = key
      };

      var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest);

      return await Task.FromResult(getObjectResponse.ResponseStream);
    }
    catch (AmazonS3Exception e)
    {
      Console.WriteLine($"Error getting file from {_bucketName}: {e.Message}");
      return null;
    }
  }

  public async Task Put(string key, string contentType, Stream stream)
  {
    try
    {
      await EnsureBucketExistsAsync();
      var uploadRequest = new TransferUtilityUploadRequest
      {
        InputStream = stream,
        Key = key,
        BucketName = _bucketName,
        ContentType = contentType
      };

      var fileTransferUtility = new TransferUtility(_s3Client);
      await fileTransferUtility.UploadAsync(uploadRequest);
    }
    catch (AmazonS3Exception e)
    {
      Console.WriteLine($"Error uploading file to {_bucketName}: {e.Message}");
    }
  }

  private async Task EnsureBucketExistsAsync()
  {
    try
    {
      var response = await _s3Client.ListBucketsAsync();
      if (!response.Buckets.Exists(b => b.BucketName == _bucketName))
      {
        // Create bucket if it does not exist
        await _s3Client.PutBucketAsync(new PutBucketRequest
        {
          BucketName = _bucketName
        });
      }
    }
    catch (AmazonS3Exception e)
    {
      Console.WriteLine($"Error accessing bucket {_bucketName}: {e.Message}");
    }
  }
}