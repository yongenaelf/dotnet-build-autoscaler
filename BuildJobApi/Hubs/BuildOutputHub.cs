using Microsoft.AspNetCore.SignalR;

namespace BuildJobApi.Hubs
{
  public class BuildOutputHub : Hub
  {
    public Task SendMessage(string message)
    {
      return Clients.All.SendAsync("ReceiveMessage", message);
    }
  }
}