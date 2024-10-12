namespace Shared.Interfaces;

public interface IVirusScanService
{
  Task ScanAsync(Stream stream);
}