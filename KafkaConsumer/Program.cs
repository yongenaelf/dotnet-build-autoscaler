using System;
using Confluent.Kafka;

class Program
{
    public static void Main(string[] args)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "kafka:9092",
            GroupId = "build-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        
        consumer.Subscribe("build_jobs");

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