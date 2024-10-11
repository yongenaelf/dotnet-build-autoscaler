using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Confluent.Kafka;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

// Define the Kafka configuration
var kafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
var kafkaConfig = new ProducerConfig { BootstrapServers = kafkaBootstrapServers };

// Configure AWS S3 client with MinIO settings
var s3Client = new AmazonS3Client(
    Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? "admin", // Access key
    Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? "password", // Secret key
    new AmazonS3Config
    {
        ServiceURL = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "http://localhost:9000", // MinIO server URL
        ForcePathStyle = true // Use path style URLs
    });

var bucketName = Environment.GetEnvironmentVariable("MINIO_BUCKET_NAME") ?? "job-requests"; // The name of the bucket

// Ensure the bucket exists or create it if it doesn't
async Task EnsureBucketExistsAsync()
{
    try
    {
        var response = await s3Client.ListBucketsAsync();
        if (!response.Buckets.Exists(b => b.BucketName == bucketName))
        {
            // Create bucket if it does not exist
            await s3Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucketName
            });
        }
    }
    catch (AmazonS3Exception e)
    {
        Console.WriteLine($"Error accessing bucket {bucketName}: {e.Message}");
    }
}

await EnsureBucketExistsAsync(); 

// Define the endpoint for uploading files
app.MapPost("/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("No file uploaded.");
    }

    // Generate a unique job ID
    var jobId = Guid.NewGuid().ToString();

    // Construct a new file name using the job ID and the .zip extension
    var fileExtension = Path.GetExtension(file.FileName);
    var newFileName = $"{jobId}{fileExtension}";

    // Construct the file path in MinIO
    var filePath = $"{bucketName}/{newFileName}";

    try
    {
        // Upload file to MinIO
        using (var stream = file.OpenReadStream())
        {
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = newFileName,
                BucketName = bucketName,
                ContentType = file.ContentType
            };

            var fileTransferUtility = new TransferUtility(s3Client);
            await fileTransferUtility.UploadAsync(uploadRequest);
        }

        // Send a Kafka message with the filename
        using (var producer = new ProducerBuilder<Null, string>(kafkaConfig).Build())
        {
            var topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "build_jobs";  // Define your Kafka topic
            var message = $"File uploaded: {newFileName}";

            var metadata = new
            {
                JobId = jobId,
                FileName = newFileName,
                UploadTime = DateTime.UtcNow
            };

            var messageWithMetadata = new
            {
                Message = message,
                Metadata = metadata
            };

            var messageValue = System.Text.Json.JsonSerializer.Serialize(messageWithMetadata);

            await producer.ProduceAsync(topic, new Message<Null, string> { Value = messageValue });
            // Use synchronous Flush to ensure delivery
            producer.Flush(TimeSpan.FromSeconds(10));
        }

        return Results.Ok(new { Message = "File uploaded successfully", JobId = jobId, FilePath = filePath });
    }
    catch (Exception ex)
    {
        // Return an object result with error message and status code
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Internal Server Error");
    }
}).DisableAntiforgery();

app.Run();
