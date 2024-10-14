namespace BuildJobApi.Interfaces;

public interface IVirusScanService
{
  Task ScanAsync(Stream stream);
}