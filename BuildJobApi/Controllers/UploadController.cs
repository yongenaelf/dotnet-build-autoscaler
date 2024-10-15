using Microsoft.AspNetCore.Mvc;
using BuildJobShared.Interfaces;

namespace BuildJobApi.Controllers;

[ApiController]
public class UploadController(IObjectStorageService objectStorageService, IVirusScanService virusScanService) : ControllerBase
{
  [HttpPost]
  [Route("upload")]
  public async Task<IActionResult> UploadFile(IFormFile file)
  {
    if (file == null || file.Length == 0)
    {
      return BadRequest("No file uploaded.");
    }

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
      return Ok(new { Message = "File uploaded successfully", JobId = jobId, FilePath = newFileName });
    }
    catch (Exception e)
    {
      Console.WriteLine(e);
      return StatusCode(500, e.Message);
    }
  }
}