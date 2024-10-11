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

    var producerConfig = new ProducerConfig { BootstrapServers = kafkaBroker };
    using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();

    try
    {
      while (true)
      {
        var cr = consumer.Consume();
        Console.WriteLine($"Consumed message '{cr.Message.Value}' at: '{cr.TopicPartitionOffset}'.");

        var messageValue = System.Text.Json.JsonSerializer.Deserialize<KafkaMessage>(cr.Message.Value);

        var jobId = messageValue.Metadata.JobId;
        var fileName = messageValue.Metadata.FileName;
        var command = messageValue.Metadata.Command;

        // get the file from minio
        var getObjectRequest = new GetObjectRequest
        {
          BucketName = bucketName,
          Key = fileName
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

            if (command == "test")
            {
              var csprojTestFile = Directory.GetFiles(extractPath, "*.Tests.csproj", SearchOption.AllDirectories).FirstOrDefault();

              if (csprojTestFile != null)
              {
                csprojFile = csprojTestFile;
              }
              else
              {
                csprojFile = null;
                Console.WriteLine("No .Tests.csproj file found in the extracted archive.");
              }
            }

            if (csprojFile != null)
            {
              var processInfo = new ProcessStartInfo("dotnet", $"{command} \"{csprojFile}\"")
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

                  producer.Produce(jobId + "_output", new Message<Null, string> { Value = args.Data });

                  if (args.Data != null && args.Data.Contains(".dll.patched"))
                  {
                    var patchedDllPath = Path.Combine(Path.GetDirectoryName(csprojFile), args.Data.Trim());
                    if (File.Exists(patchedDllPath))
                    {
                      var patchedDllBytes = File.ReadAllBytes(patchedDllPath);
                      var patchedDllBase64 = Convert.ToBase64String(patchedDllBytes);

                      var resultMessage = new KafkaMessage
                      {
                        Message = patchedDllBase64,
                        Metadata = new KafkaMetadata
                        {
                          JobId = jobId,
                          FileName = fileName,
                          UploadTime = DateTime.UtcNow,
                          Command = command
                        }
                      };

                      var resultMessageJson = System.Text.Json.JsonSerializer.Serialize(resultMessage);
                      producer.Produce(jobId + "_success", new Message<Null, string> { Value = resultMessageJson });
                    }
                    else
                    {
                      Console.WriteLine("Patched DLL not found.");
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

        if (command != "share") {
          // Delete the file from MinIO
          var deleteObjectRequest = new DeleteObjectRequest
          {
            BucketName = bucketName,
            Key = fileName
          };

          s3Client.DeleteObjectAsync(deleteObjectRequest).Wait();
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

public class KafkaMessage
{
  public string Message { get; set; }
  public KafkaMetadata Metadata { get; set; }
}

public class KafkaMetadata
{
  public string JobId { get; set; }
  public string FileName { get; set; }
  public DateTime UploadTime { get; set; }
  public string Command { get; set; }
}