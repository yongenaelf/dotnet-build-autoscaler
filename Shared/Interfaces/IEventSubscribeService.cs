using System.Net.WebSockets;
using Confluent.Kafka;

namespace Shared.Interfaces;

public interface IEventSubscribeService
{
  IConsumer<string, string> GetConsumer();
  void Dispose();
  Task SubscribeAsync<T>(string topic, Func<T?, Task> handler, CancellationToken cancellationToken);
  Task SubscribeAsync(string topic, WebSocket webSocket, CancellationToken cancellationToken);
}