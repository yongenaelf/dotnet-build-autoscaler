using BuildJobApi.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace BuildJobApi.Hubs
{
  public class BuildOutputHub : Hub<IBuildOutputHub>
  {
    public Task SendMessageToGroup(string groupName, string message)
    {
      return Clients.Group(groupName).ReceiveMessage(message);
    }

    public Task AddToGroup(string groupName)
    {
      return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
  }
}