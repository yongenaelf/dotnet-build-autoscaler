namespace BuildJobShared.Interfaces;

public interface IVirusScanService
{
  Task ScanAsync(Stream stream);
}