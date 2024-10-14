using Amazon.S3;
using Confluent.Kafka;
using System.Diagnostics;
using System.IO.Compression;
using Shared.Models;
using Shared.Services;
using System.Text.RegularExpressions;

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
var kafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
var kafkaConfig = new ProducerConfig { BootstrapServers = kafkaBootstrapServers };
var eventPublishService = new EventPublishService(kafkaConfig);

var kafkaConsumerConfig = new ConsumerConfig
{
  GroupId = "build-consumer-group",
  BootstrapServers = kafkaBootstrapServers,
  AutoOffsetReset = AutoOffsetReset.Latest
};
var eventSubscribeService = new EventSubscribeService(kafkaConsumerConfig);
#endregion

var kafkaTopic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "build-jobs";
var kafkaOutputTopic = Environment.GetEnvironmentVariable("KAFKA_OUTPUT_TOPIC") ?? "build-jobs-output";
var cancellationToken = new CancellationTokenSource();

try
{
  await eventSubscribeService.SubscribeAsync<KafkaMessage>(kafkaTopic, async message =>
  {
    if (message == null)
    {
      Console.WriteLine("Message is null.");
      return;
    }

    var jobId = message.Metadata.JobId;
    var fileName = message.Metadata.FileName;
    var command = message.Metadata.Command;

    Console.WriteLine($"Starting: {fileName} for job {jobId} with command {command}");

    if (new List<string> { "build", "test" }.Contains(command) == false)
    {
      Console.WriteLine("Neither build nor test. Skipping.");
      return;
    }

    Console.WriteLine($"Downloading {fileName} from object storage.");
    var stream = await objectStorageService.Get(fileName);

    if (stream == null)
    {
      Console.WriteLine("File not found.");
      return;
    }

    Console.WriteLine($"Downloaded {fileName} from object storage.");

    var archive = new ZipArchive(stream);

    var extractPath = Path.Combine(Path.GetTempPath(), jobId);

    // if path exists, delete it
    if (Directory.Exists(extractPath))
    {
      Directory.Delete(extractPath, true);
    }

    Console.WriteLine($"Extracting {fileName} to {extractPath}.");
    ZipFileExtensions.ExtractToDirectory(archive, extractPath);

    var extension = command == "test" ? ".Tests.csproj" : ".csproj";

    Console.WriteLine($"Searching for {extension} file in the extracted archive.");
    var csprojFile = Directory.GetFiles(extractPath, $"*{extension}", SearchOption.AllDirectories).FirstOrDefault();

    if (csprojFile == null)
    {
      Console.WriteLine($"No {extension} file found in the extracted archive.");
      return;
    }

    Console.WriteLine($"Starting dotnet ${command} for ${csprojFile}.");

    var processInfo = new ProcessStartInfo("dotnet", $"{command} \"{csprojFile}\"")
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using (var process = new Process { StartInfo = processInfo })
    {
      if (process == null)
      {
        Console.WriteLine("Failed to start the process.");
        return;
      }

      process.OutputDataReceived += (sender, args) =>
      {
        if (args.Data != null)
        {
          Console.WriteLine(args.Data);

          KafkaMessage outputMessage = new KafkaMessage
          {
            Message = args.Data,
            Metadata = new KafkaMetadata
            {
              JobId = jobId,
              FileName = fileName,
              UploadTime = DateTime.UtcNow,
              Command = command
            }
          };

          if (args.Data.Contains(".dll.patched"))
          {
            string pattern = @"Saving as (.+)$";
            string input = args.Data.Trim();
            var match = Regex.Match(input, pattern);

            var patchedDllPath = match.Groups[1].Value;

            if (patchedDllPath != null && File.Exists(patchedDllPath))
            {
              var patchedDllBytes = File.ReadAllBytes(patchedDllPath);
              var patchedDllBase64 = Convert.ToBase64String(patchedDllBytes);

              outputMessage = new KafkaMessage
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
            }
            else
            {
              Console.WriteLine("Patched DLL not found.");
            }
          }
          else
          {
            outputMessage = new KafkaMessage
            {
              Message = args.Data,
              Metadata = new KafkaMetadata
              {
                JobId = jobId,
                FileName = fileName,
                UploadTime = DateTime.UtcNow,
                Command = command
              }
            };
          }

          eventPublishService.PublishAsync(kafkaOutputTopic, outputMessage, jobId).Wait();
        }
      };
      process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();
      process.WaitForExit();
    }

    Console.WriteLine($"Deleting {fileName} from object storage.");
    await objectStorageService.Delete(fileName);

    Console.WriteLine("Job completed.");

  }, cancellationToken.Token);
}
catch (Exception e)
{
  Console.WriteLine(e.Message);
}