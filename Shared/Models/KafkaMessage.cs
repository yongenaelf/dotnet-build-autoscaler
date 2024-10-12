namespace Shared.Models;

public class KafkaMessage
{
  public required string Message { get; set; }
  public required KafkaMetadata Metadata { get; set; }
}