using BuildJobApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using BuildJobApi.Interfaces;

namespace BuildJobApi.Services;

public class HubCallerService(IHubContext<BuildOutputHub> buildOutputHubContext) : IHubCallerService
{

  public async Task SendMessage(string message)
  {
    await buildOutputHubContext.Clients.All.SendAsync("ReceiveMessage", message);
  }
}