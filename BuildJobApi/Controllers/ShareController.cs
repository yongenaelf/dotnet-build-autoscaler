using Microsoft.AspNetCore.Mvc;
using Shared.Interfaces;

namespace BuildJobApi.Controllers;

[ApiController]
public class ShareController(IObjectStorageService objectStorageService) : ControllerBase
{
  private readonly IObjectStorageService _objectStorageService = objectStorageService;

  [HttpGet]
  [Route("share/{fileName}")]
  public async Task<IActionResult> GetShare(string fileName)
  {
    var file = await _objectStorageService.Get(fileName);

    if (file == null)
    {
      return NotFound();
    }

    return File(file, "application/octet-stream");
  }
}