using BuildJobApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using BuildJobApi.Interfaces;

namespace BuildJobApi.Services;

public class HubCallerService(IHubContext<BuildOutputHub> buildOutputHubContext) : IHubCallerService
{

  public async Task SendMessageToGroup(string group, string message)
  {
    await buildOutputHubContext.Clients.Group(group).SendAsync("ReceiveMessage", message);
  }
}