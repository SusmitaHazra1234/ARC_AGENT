using Microsoft.AspNetCore.Mvc;
using ARC.Api.Auth;
using ARC.Api.DTOs;
using ARC.Api.Services;

namespace ARC.Api.Controllers;

[ApiController]
[Route("v1/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly ChatOrchestrator _chat;

    public ChatController(ChatOrchestrator chat) => _chat = chat;

    [HttpPost("messages")]
    public async Task<IActionResult> PostMessage(
        [FromBody] ChatMessageRequest body,
        CancellationToken cancellationToken)
    {
        var actor = ArcActorHttp.GetRequired(HttpContext);
        var response = await _chat.HandleAsync(body, actor, cancellationToken);
        return Ok(response);
    }
}
