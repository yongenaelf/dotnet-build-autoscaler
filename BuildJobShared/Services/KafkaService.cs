using System.Text.Json;
using BuildJobShared.Interfaces;
using BuildJobShared.Models;
using BuildJobShared.Settings;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace BuildJobShared.Services;

public class KafkaService : IKafkaService
{
  private readonly IConfiguration Configuration;
  private readonly string _bootstrapServers;
  private readonly string _groupId;
  private readonly string _jobRequestTopic;
  private readonly string _jobLogTopic;
  private readonly string _jobResultTopic;

  public KafkaService(IConfiguration configuration)
  {
    Configuration = configuration;

    var config = new KafkaSettings();
    Configuration.GetSection("Kafka").Bind(config);

    _bootstrapServers = config.BootstrapServers;
    _groupId = config.GroupId;
    _jobRequestTopic = config.JobRequestTopic;
    _jobLogTopic = config.JobLogTopic;
    _jobResultTopic = config.JobResultTopic;

    // Create topics if they do not exist
    using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _bootstrapServers }).Build();
    adminClient.CreateTopicsAsync(new List<TopicSpecification>
    {
      new() { Name = _jobRequestTopic, NumPartitions = 1, ReplicationFactor = 1 },
      new() { Name = _jobLogTopic, NumPartitions = 1, ReplicationFactor = 1 },
      new() { Name = _jobResultTopic, NumPartitions = 1, ReplicationFactor = 1 },
    });
  }
  private async Task Publish(string topic, string message)
  {
    using var producer = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = _bootstrapServers }).Build();
    await producer.ProduceAsync(topic, new Message<Null, string> { Value = message });
  }

  private async Task Subscribe(string topic, Action<string> callback)
  {
    using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig { BootstrapServers = _bootstrapServers, GroupId = _groupId }).Build();
    consumer.Subscribe(topic);
    while (true)
    {
      try
      {
        var consumeResult = consumer.Consume();
        await Task.Run(() => callback(consumeResult.Message.Value));
      }
      catch (ConsumeException e)
      {
        Console.WriteLine($"Error occurred: {e.Error.Reason}");
      }
    }
  }

  public async Task PublishJobRequest(string jobId, string command)
  {
    var message = JsonSerializer.Serialize(new KafkaDto { Id = jobId, Message = command });
    await Publish(_jobRequestTopic, message);
  }

  public async Task SubscribeJobLog(Action<string> callback)
  {
    await Subscribe(_jobLogTopic, callback);
  }

  public async Task SubscribeJobResult(Action<string> callback)
  {
    await Subscribe(_jobResultTopic, callback);
  }
}