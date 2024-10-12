namespace Shared.Models;

public class KafkaMetadata
{
  public required string JobId { get; set; }
  public required string FileName { get; set; }
  public DateTime UploadTime { get; set; }
  public required string Command { get; set; }
}