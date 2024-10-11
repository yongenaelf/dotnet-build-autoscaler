using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Confluent.Kafka;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

class Program
{
  public static void Main(string[] args)
  {
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

    var kafkaBroker = Environment.GetEnvironmentVariable("KAFKA_BROKER") ?? "kafka:9092";
    var kafkaConsumerGroup = Environment.GetEnvironmentVariable("KAFKA_CONSUMER_GROUP") ?? "build-consumer-group";
    var config = new ConsumerConfig
    {
      BootstrapServers = kafkaBroker,
      GroupId = kafkaConsumerGroup,
      AutoOffsetReset = AutoOffsetReset.Earliest
    };

    using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

    var kafkaTopic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "build_jobs";
    consumer.Subscribe(kafkaTopic);

    try
    {
      while (true)
      {
        var cr = consumer.Consume();
        Console.WriteLine($"Consumed message '{cr.Message.Value}' at: '{cr.TopicPartitionOffset}'.");

        var messageValue = System.Text.Json.JsonSerializer.Deserialize<dynamic>(cr.Message.Value);
        var jobId = messageValue.Metadata.JobId;
        var fileName = messageValue.Metadata.FileName;

        // Construct the file path in MinIO
        var filePath = $"{bucketName}/{fileName}";

        // get the file from minio
        var getObjectRequest = new GetObjectRequest
        {
          BucketName = bucketName,
          Key = filePath
        };

        var getObjectResponse = s3Client.GetObjectAsync(getObjectRequest).Result;

        // Process the file
        using (var stream = getObjectResponse.ResponseStream)
        {
          using (var archive = new System.IO.Compression.ZipArchive(stream))
          {
            var extractPath = Path.Combine(Path.GetTempPath(), jobId);
            ZipFileExtensions.ExtractToDirectory(archive, extractPath);

            var csprojFile = Directory.GetFiles(extractPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
            if (csprojFile != null)
            {
              var processInfo = new ProcessStartInfo("dotnet", $"build \"{csprojFile}\"")
              {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
              };

              using (var process = Process.Start(processInfo))
              {
                process.OutputDataReceived += (sender, args) => 
                {
                  Console.WriteLine(args.Data);
                  if (args.Data != null && args.Data.Contains(".dll.patched"))
                  {
                    var patchedDllPath = Path.Combine(Path.GetDirectoryName(csprojFile), args.Data.Trim());
                    if (File.Exists(patchedDllPath))
                    {
                      var uploadRequest = new TransferUtilityUploadRequest
                      {
                        BucketName = bucketName,
                        Key = $"{jobId}-dll",
                        FilePath = patchedDllPath
                      };

                      var fileTransferUtility = new TransferUtility(s3Client);
                      fileTransferUtility.Upload(uploadRequest);
                      Console.WriteLine($"Patched DLL uploaded to MinIO as {jobId}-dll.");
                    }
                  }
                };
                process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
              }
            }
            else
            {
              Console.WriteLine("No .csproj file found in the extracted archive.");
            }
          }
        }
      }
    }
    catch (Exception e)
    {
      Console.WriteLine($"Error: {e.Message}");
      consumer.Close();
    }
  }
}