using BuildJobApi.Interfaces;
using Shared.Interfaces;
using Shared.Models;

namespace BuildJobApi.Services;

public sealed class BuildOutputBackgroundService(IEventSubscribeService eventSubscribeService, IHubCallerService hubCallerService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await eventSubscribeService.SubscribeAsync<KafkaMessage>("build_jobs_output", async (message) =>
        {
            var jobId = message?.Metadata.JobId;
            if (jobId != null)
            {
                await hubCallerService.SendMessageToGroup(jobId, message?.Message ?? "Message is null");
            }
        }, stoppingToken);
    }
}