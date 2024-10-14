using BuildJobApi.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BuildJobApi.Hubs
{
  public class BuildOutputHub(IBuildService buildService) : Hub<IBuildOutputHub>
  {
    public Task SendMessageToGroup(string groupName, string message)
    {
      return Clients.Group(groupName).ReceiveMessage(message);
    }

    public Task AddToGroup(string groupName)
    {
      return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public Task StartBuild(string jobId, string command)
    {
      return buildService.ProcessBuild(Context.ConnectionId, jobId, command);
    }
  }
}