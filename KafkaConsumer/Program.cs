using Amazon.S3;
using Confluent.Kafka;
using System.Diagnostics;
using System.IO.Compression;
using Shared.Models;
using Shared.Services;

#region ObjectStorageService configuration
var awsAccessKeyId = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? "minio";
var awsSecretAccessKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? "minio123";
var clientConfig = new AmazonS3Config
{
  ServiceURL = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "http://localhost:9000",
  ForcePathStyle = true
};
var bucketName = Environment.GetEnvironmentVariable("MINIO_BUCKET_NAME") ?? "job-requests";
var objectStorageService = new ObjectStorageService(awsAccessKeyId, awsSecretAccessKey, clientConfig, bucketName);
#endregion

#region Event Bus configuration
var kafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9093";
var kafkaConfig = new ProducerConfig { BootstrapServers = kafkaBootstrapServers };
var eventPublishService = new EventPublishService(kafkaConfig);

var kafkaConsumerConfig = new ConsumerConfig
{
  GroupId = "build-consumer-group",
  BootstrapServers = kafkaBootstrapServers,
  AutoOffsetReset = AutoOffsetReset.Earliest
};
var eventSubscribeService = new EventSubscribeService(kafkaConsumerConfig);
#endregion

var kafkaTopic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "build_jobs";

try
{
  await eventSubscribeService.SubscribeAsync<KafkaMessage>(kafkaTopic, async message =>
  {
    var jobId = message.Metadata.JobId;
    var fileName = message.Metadata.FileName;
    var command = message.Metadata.Command;

    if (command != "build" || command != "test")
    {
      Console.WriteLine("Neither build nor test. Skipping.");
      return;
    }

    var stream = await objectStorageService.Get(fileName);

    if (stream == null)
    {
      Console.WriteLine("File not found.");
      return;
    }

    var archive = new ZipArchive(stream);

    var extractPath = Path.Combine(Path.GetTempPath(), jobId);
    ZipFileExtensions.ExtractToDirectory(archive, extractPath);

    var extension = command == "test" ? ".Tests.csproj" : ".csproj";

    var csprojFile = Directory.GetFiles(extractPath, $"*{extension}", SearchOption.AllDirectories).FirstOrDefault();

    if (csprojFile == null)
    {
      Console.WriteLine($"No {extension} file found in the extracted archive.");
      return;
    }

    var processInfo = new ProcessStartInfo("dotnet", $"{command} \"{csprojFile}\"")
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    var process = Process.Start(processInfo);

    if (process == null)
    {
      Console.WriteLine("Failed to start the process.");
      return;
    }

    process.OutputDataReceived += (sender, args) =>
    {
      Console.WriteLine(args.Data);

      eventPublishService.PublishAsync(jobId + "_output", args.Data).Wait();

      if (args.Data != null && args.Data.Contains(".dll.patched"))
      {
        var patchedDllPath = Path.Combine(Path.GetDirectoryName(csprojFile) ?? "", args.Data.Trim());
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

          eventPublishService.PublishAsync(jobId + "_success", resultMessageJson).Wait();
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

    Directory.Delete(extractPath, true);
    stream.Dispose();
    await objectStorageService.Delete(fileName);
  });
}
catch (Exception e)
{
  Console.WriteLine(e.Message);
}