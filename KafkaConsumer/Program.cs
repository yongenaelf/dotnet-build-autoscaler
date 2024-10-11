using System;
using Confluent.Kafka;

class Program
{
    public static void Main(string[] args)
    {
        var kafkaBroker = Environment.GetEnvironmentVariable("KAFKA_BROKER") ?? "kafka:9092";
        var kafkaConsumerGroup = Environment.GetEnvironmentVariable("KAFKA_CONSUMER_GROUP") ?? "build-consumer-group";
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaBroker,
            GroupId = kafkaConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        
        var kafkaTopic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "build_jobs";
        consumer.Subscribe(kafkaTopic);

        try
        {
            while (true)
            {
                var cr = consumer.Consume();
                Console.WriteLine($"Consumed message '{cr.Value}' at: '{cr.TopicPartitionOffset}'.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            consumer.Close();
        }
    }
}