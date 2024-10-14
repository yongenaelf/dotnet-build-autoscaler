using BuildJobApi.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Shared.Interfaces;
using Shared.Models;

namespace BuildJobApi.Controllers;

[ApiController]
public class UploadController(IObjectStorageService objectStorageService, IEventPublishService eventPublishService, IVirusScanService virusScanService, IHubCallerService hubCallerService) : ControllerBase
{
  private readonly string _topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "build_jobs";

  [HttpPost]
  [Route("upload")]
  public async Task<IActionResult> UploadFile(IFormFile file, string? command = "build")
  {
    if (file == null || file.Length == 0)
    {
      return BadRequest("No file uploaded.");
    }

    // Generate a unique job ID
    var jobId = Guid.NewGuid().ToString();

    // Construct a new file name using the job ID and the .zip extension
    var fileExtension = Path.GetExtension(file.FileName);
    var newFileName = $"{jobId}{fileExtension}";

    try
    {
      using var stream = file.OpenReadStream();

      // Scan the file for viruses on production
      if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
      {
        await virusScanService.ScanAsync(stream);
      }

      await objectStorageService.Put(newFileName, file.ContentType, stream);

      var message = $"File uploaded: {newFileName}";

      if (command == "share")
      {
        return Ok(new { Message = "File uploaded successfully", JobId = jobId, FilePath = newFileName });
      }

      var messageWithMetadata = new KafkaMessage
      {
        Message = message,
        Metadata = new KafkaMetadata
        {
          JobId = jobId,
          FileName = newFileName,
          UploadTime = DateTime.UtcNow,
          Command = command ?? "build"
        }
      };

      await eventPublishService.PublishAsync(_topic, messageWithMetadata);
      await hubCallerService.SendMessageToGroup(jobId, message);

      return Ok(new { Message = "File uploaded successfully", JobId = jobId, FilePath = newFileName });
    }
    catch (Exception e)
    {
      Console.WriteLine(e);
      return StatusCode(500, e.Message);
    }
  }
}