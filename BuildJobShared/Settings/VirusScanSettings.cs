namespace BuildJobShared.Settings;
public sealed class VirusScanSettings
{
  public const string ClamAV = "ClamAV";
  public string Host { get; set; } = String.Empty;
}
