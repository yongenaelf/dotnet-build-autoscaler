using System.Text.Json;
using Amazon.S3;
using Confluent.Kafka;
using System.IO.Compression;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using BuildJobShared.Settings;
using BuildJobShared.Models;

#region Bind hierarchical configuration
// https://learn.microsoft.com/en-us/dotnet/core/extensions/options#bind-hierarchical-configuration
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Sources.Clear();

IHostEnvironment env = builder.Environment;

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true);
#endregion

#region ObjectStorageService configuration
ObjectStorageSettings objectStorageSettings = new();
builder.Configuration.GetSection("S3").Bind(objectStorageSettings);
var clientConfig = new AmazonS3Config
{
  ServiceURL = objectStorageSettings.Endpoint,
  ForcePathStyle = true
};
var bucketName = objectStorageSettings.BucketName;
IAmazonS3 s3Client = new AmazonS3Client(objectStorageSettings.AccessKey, objectStorageSettings.SecretKey, clientConfig);
#endregion

#region Kafka configuration
KafkaSettings kafkaSettings = new();
builder.Configuration.GetSection("Kafka").Bind(kafkaSettings);
var producerConfig = new ProducerConfig
{
  BootstrapServers = kafkaSettings.BootstrapServers,
};
var consumerConfig = new ConsumerConfig
{
  BootstrapServers = kafkaSettings.BootstrapServers,
  GroupId = kafkaSettings.GroupId,
  AutoOffsetReset = AutoOffsetReset.Latest,
  EnableAutoCommit = false
};

var jobRequestTopic = kafkaSettings.JobRequestTopic;
var jobLogTopic = kafkaSettings.JobLogTopic;
var jobResultTopic = kafkaSettings.JobResultTopic;
#endregion

using IHost host = builder.Build();

using (var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build())
{
  consumer.Subscribe(jobRequestTopic);

  while (true)
  {
    var consumeResult = consumer.Consume(CancellationToken.None);

    if (consumeResult != null)
    {
      await ProcessJob(consumeResult, consumer);
    }
  }
}

async Task ProcessJob(ConsumeResult<Ignore, string> consumeResult, IConsumer<Ignore, string>? consumer)
{
  // deserialize the message
  var value = consumeResult.Message.Value;
  var job = JsonSerializer.Deserialize<KafkaDto>(value);

  if (job == null)
  {
    Console.WriteLine("Failed to deserialize the message.");
    return;
  }

  var jobId = job.Id;
  var command = job.Message;

  Console.WriteLine($"Starting: Job {jobId} with command {command}");

  if (new List<string> { "build", "test" }.Contains(command) == false)
  {
    Console.WriteLine("Neither build nor test. Skipping.");
    return;
  }

  Console.WriteLine($"Downloading {jobId} from object storage.");
  var stream = s3Client.GetObjectAsync(bucketName, jobId + ".zip").Result.ResponseStream;

  if (stream == null)
  {
    Console.WriteLine("File not found.");
    return;
  }

  Console.WriteLine($"Downloaded {jobId} from object storage.");

  var archive = new ZipArchive(stream);

  var extractPath = Path.Combine(Path.GetTempPath(), jobId);

  // if path exists, delete it
  if (Directory.Exists(extractPath))
  {
    Directory.Delete(extractPath, true);
  }

  Console.WriteLine($"Extracting to {extractPath}.");
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

        // send to producer
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        producer.Produce(jobLogTopic, new Message<string, string> { Key = jobId, Value = JsonSerializer.Serialize(new KafkaDto { Id = jobId, Message = args.Data }) });
        producer.Flush(TimeSpan.FromSeconds(10));
      }
    };
    process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.Exited += (sender, args) =>
    {
      Console.WriteLine("Process exited.");

      if (process.ExitCode == 0)
      {
        if (command == "build")
        {
          // Search for a file with extension .dll.patched in the extracted folder
          var patchedDllPath = Directory.GetFiles(extractPath, "*.dll.patched", SearchOption.AllDirectories).FirstOrDefault();

          if (patchedDllPath != null)
          {
            var dllBytes = File.ReadAllBytes(patchedDllPath);
            var dllBase64 = Convert.ToBase64String(dllBytes);

            // send to producer
            using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
            producer.Produce(jobResultTopic, new Message<string, string> { Key = jobId, Value = JsonSerializer.Serialize(new KafkaDto { Id = jobId, Message = dllBase64 }) });
            producer.Flush(TimeSpan.FromSeconds(10));
          }
          else
          {
            Console.WriteLine("DLL not found.");
          }
        }
      }
    };
    process.WaitForExit();
  }

  Console.WriteLine("Job completed.");
  consumer.Commit(consumeResult);
}