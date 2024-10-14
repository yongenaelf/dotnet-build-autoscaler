using ClamAV.Net.Client;
using ClamAV.Net.Client.Results;
using BuildJobApi.Interfaces;

namespace BuildJobApi.Services;

public class VirusScanService(string connectionString) : IVirusScanService
{
  private readonly IClamAvClient _clamAvClient = ClamAvClient.Create(new Uri(connectionString));
  public async Task ScanAsync(Stream stream)
  {
    ScanResult res = await _clamAvClient.ScanDataAsync(stream).ConfigureAwait(false);

    if (res.Infected)
    {
      throw new Exception($"File is infected with virus: {res.VirusName}");
    }
  }
}