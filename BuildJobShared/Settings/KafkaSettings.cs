namespace BuildJobShared.Settings;
public sealed class KafkaSettings
{
  public const string Kafka = "Kafka";
  public string BootstrapServers { get; set; } = String.Empty;
  public string GroupId { get; set; } = String.Empty;
  public string JobRequestTopic { get; set; } = String.Empty;
  public string JobLogTopic { get; set; } = String.Empty;
  public string JobResultTopic { get; set; } = String.Empty;
}
