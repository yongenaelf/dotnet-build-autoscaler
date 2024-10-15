using BuildJobShared.Interfaces;
using BuildJobShared.Settings;
using ClamAV.Net.Client;
using ClamAV.Net.Client.Results;

namespace BuildJobShared.Services;

public class VirusScanService : IVirusScanService
{
  private readonly IConfiguration Configuration;
  private readonly IClamAvClient _clamAvClient;

  public VirusScanService(IConfiguration configuration)
  {
    Configuration = configuration;

    var config = new VirusScanSettings();
    Configuration.GetSection("ClamAV").Bind(config);

    if (string.IsNullOrEmpty(config.Host))
    {
      throw new Exception("ClamAV host is not configured");
    }

    _clamAvClient = ClamAvClient.Create(new Uri(config.Host));
  }
  public async Task ScanAsync(Stream stream)
  {
    ScanResult res = await _clamAvClient.ScanDataAsync(stream).ConfigureAwait(false);

    if (res.Infected)
    {
      throw new Exception($"File is infected with virus: {res.VirusName}");
    }
  }
}