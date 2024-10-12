using System.Net.WebSockets;
using System.Text;
using Confluent.Kafka;
using Shared.Interfaces;

namespace Shared.Services;

public class EventSubscribeService : IEventSubscribeService
{
  private readonly IConsumer<Ignore, string> _consumer;

  public EventSubscribeService(ConsumerConfig consumerConfig)
  {
    _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
  }
  public async Task SubscribeAsync<T>(string topic, Func<T, Task> handler)
  {
    _consumer.Subscribe(topic);

    while (true)
    {
      var consumeResult = _consumer.Consume();
      var message = System.Text.Json.JsonSerializer.Deserialize<T>(consumeResult.Message.Value);
      if (message != null)
      {
        await handler(message);
      }
    }
  }

  public async Task SubscribeAsync(string topic, WebSocket webSocket, CancellationToken cancellationToken)
  {
    _consumer.Subscribe(topic);

    while (!cancellationToken.IsCancellationRequested)
    {
      var result = _consumer.Consume(cancellationToken);
      if (result?.Message != null)
      {
        var message = result.Message.Value;
        var buffer = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);
      }
    }
  }
}