namespace BuildJobShared.Settings;
public sealed class ObjectStorageSettings
{
  public const string S3 = "S3";
  public string AccessKey { get; set; } = String.Empty;
  public string SecretKey { get; set; } = String.Empty;
  public string Endpoint { get; set; } = String.Empty;
  public string BucketName { get; set; } = String.Empty;
}
