
using System.Text.Json;
using Confluent.Kafka;
using Shared.Interfaces;

namespace Shared.Services;

public class EventPublishService(ProducerConfig producerConfig) : IEventPublishService
{
  private readonly ProducerConfig _producerConfig = producerConfig;

  public async Task PublishAsync<T>(string topic, T message)
  {
    var serializedMessage = JsonSerializer.Serialize(message);
    var _producer = new ProducerBuilder<string, string>(_producerConfig).Build();

    try
    {
      var deliveryResult = await _producer.ProduceAsync(topic, new Message<string, string> { Value = serializedMessage });
    }
    catch (ProduceException<string, T> e)
    {
      Console.WriteLine($"Delivery failed: {e.Error.Reason}");
    }
  }
}