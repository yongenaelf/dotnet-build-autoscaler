using System.Net.WebSockets;

namespace Shared.Interfaces;

public interface IEventSubscribeService
{
  Task SubscribeAsync<T>(string topic, Func<T, Task> handler);
  Task SubscribeAsync(string topic, WebSocket webSocket, CancellationToken cancellationToken);
}