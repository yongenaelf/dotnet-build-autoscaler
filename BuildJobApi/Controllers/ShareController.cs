using Microsoft.AspNetCore.Mvc;
using Shared.Interfaces;

namespace BuildJobApi.Controllers;

[ApiController]
public class ShareController : ControllerBase
{
  private readonly IObjectStorageService _objectStorageService;

  public ShareController(IObjectStorageService objectStorageService)
  {
    _objectStorageService = objectStorageService;
  }

  [HttpGet]
  [Route("share/{jobId}")]
  public async Task<IActionResult> GetShare(string jobId)
  {
    var file = await _objectStorageService.Get(jobId);

    if (file == null)
    {
      return NotFound();
    }

    return File(file, "application/octet-stream");
  }
}