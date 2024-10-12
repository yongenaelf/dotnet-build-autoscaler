
using Confluent.Kafka;
using Shared.Interfaces;

namespace Shared.Services;

public class EventPublishService(ProducerConfig producerConfig) : IEventPublishService
{
  private readonly ProducerConfig _producerConfig = producerConfig;

  public async Task PublishAsync<T>(string topic, T message)
  {
    var _producer = new ProducerBuilder<string, T>(_producerConfig).Build();

    try
    {
      var deliveryResult = await _producer.ProduceAsync(topic, new Message<string, T> { Value = message });
    }
    catch (ProduceException<string, T> e)
    {
      Console.WriteLine($"Delivery failed: {e.Error.Reason}");
    }
  }
}