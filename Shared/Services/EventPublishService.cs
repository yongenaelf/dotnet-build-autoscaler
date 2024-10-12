using Confluent.Kafka;
using Shared.Interfaces;

namespace Shared.Services;

public class EventPublishService : IEventPublishService
{
  private readonly IProducer<Null, string> _producer;
  public EventPublishService(ProducerConfig producerConfig)
  {
    _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
  }
  public async Task PublishAsync<T>(string topic, T message)
  {
    var messageValue = System.Text.Json.JsonSerializer.Serialize(message);
    await _producer.ProduceAsync(topic, new Message<Null, string> { Value = messageValue });
    _producer.Flush(TimeSpan.FromSeconds(10));
    _producer.Dispose();
  }
}