using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Shared.Interfaces;

namespace Shared.Services;

public class EventSubscribeService(ConsumerConfig consumerConfig) : IEventSubscribeService
{
  private readonly IConsumer<string, string> _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

  public async Task SubscribeAsync<T>(string topic, Func<T, Task> handler, CancellationToken cancellationToken)
  {
    _consumer.Subscribe(topic);

    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        var consumeResult = _consumer.Consume(cancellationToken);
        var value = consumeResult.Message.Value ?? throw new Exception("Value is null");

        var message = JsonSerializer.Deserialize<T>(value);
        await handler(message!);
      }
      catch (ConsumeException e)
      {
        Console.WriteLine($"Error occured: {e}");
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

  public void Dispose()
  {
    _consumer.Dispose();
  }
}