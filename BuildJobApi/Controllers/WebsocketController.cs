using Microsoft.AspNetCore.Mvc;
using Shared.Interfaces;

namespace BuildJobApi.Controllers;

[Route("ws")]
public class WebSocketController(IEventSubscribeService eventSubscribeService) : ControllerBase
{
  private readonly IEventSubscribeService _eventSubscribeService = eventSubscribeService;

  [HttpGet]
  [Route("{topic}")]
  public async Task Get(string topic)
  {
    if (HttpContext.WebSockets.IsWebSocketRequest)
    {
      using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
      var cancellationToken = HttpContext.RequestAborted;
      await _eventSubscribeService.SubscribeAsync(topic, webSocket, cancellationToken);
    }
    else
    {
      HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
  }
}