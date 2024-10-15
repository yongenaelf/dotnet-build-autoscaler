using System.Text.Json;
using BuildJobApi.Interfaces;
using BuildJobShared.Interfaces;
using BuildJobShared.Models;

namespace BuildJobApi.Services;

public class BuildService : IBuildService
{
  private readonly IHubCallerService _hubCallerService;
  private readonly IKafkaService _kafkaService;
  public BuildService(IHubCallerService hubCallerService, IKafkaService kafkaService)
  {
    _hubCallerService = hubCallerService;
    _kafkaService = kafkaService;
    Task.Run(() => ProcessJobLog());
  }
  public async Task ProcessBuild(string connectionId, string jobId, string command)
  {
    Console.WriteLine($"Starting: Job {jobId} with command {command}");
    await _kafkaService.PublishJobRequest(jobId, command);
  }

  private async Task ProcessJobLog()
  {
    await _kafkaService.SubscribeJobLog(async (message) =>
    {
      var log = JsonSerializer.Deserialize<KafkaDto>(message);
      var id = log?.Id;
      var m = log?.Message;
      await _hubCallerService.SendMessageToGroup(id, m);
    });
  }
}
