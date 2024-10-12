
using Microsoft.AspNetCore.Mvc;
using Shared.Interfaces;
using Shared.Models;

namespace BuildJobApi.Controllers;

[ApiController]
public class UploadController(IObjectStorageService objectStorageService, IEventPublishService eventPublishService) : ControllerBase
{
  private readonly IObjectStorageService _objectStorageService = objectStorageService;
  private readonly IEventPublishService _eventPublishService = eventPublishService;
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
      await _objectStorageService.Put(newFileName, file.ContentType, stream);

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

      var messageValue = System.Text.Json.JsonSerializer.Serialize(messageWithMetadata);

      await _eventPublishService.PublishAsync(_topic, messageValue);

      return Ok(new { Message = "File uploaded successfully", JobId = jobId, FilePath = newFileName });
    }
    catch (Exception e)
    {
      return StatusCode(500, e.Message);
    }
  }
}