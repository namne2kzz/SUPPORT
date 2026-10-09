using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SUPPORT.Api.Controllers.Internal.Requests;
using SUPPORT.Application.Chat.Commands.AskQuestion;
using SUPPORT.Application.Chat.DTOs;

namespace SUPPORT.Api.Controllers.Internal;

/// <summary>Streams NMate answers as server-sent events.</summary>
/// <param name="mediator">Mediator.</param>
[ApiController]
[Route("internal/v1/chat")]
public sealed class ChatController(IMediator mediator) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Asks a question; the response is <c>text/event-stream</c> with meta → delta… → citations → done (or error).</summary>
    /// <param name="request">Question and page context.</param>
    /// <param name="ct">Request-aborted token; closing the connection stops generation at the provider.</param>
    /// <returns>A task that completes when the stream ends.</returns>
    [HttpPost]
    public async Task Ask([FromBody] AskQuestionRequest request, CancellationToken ct)
    {
        var command = new AskQuestionCommand(request.ConversationId, request.Message, request.Context?.Route);
        await using var events = mediator.CreateStream(command, ct).GetAsyncEnumerator(ct);

        // Pull the first event before committing to SSE: validation, ownership and "already streaming" errors
        // surface here and still reach the client as a normal JSON error with the right status code.
        if (!await events.MoveNextAsync()) return;

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        do
        {
            await WriteAsync(events.Current, ct);
        }
        while (await events.MoveNextAsync());
    }

    private async Task WriteAsync(ChatStreamEvent evt, CancellationToken ct)
    {
        var data = JsonSerializer.Serialize(evt.Payload, evt.Payload.GetType(), Json);
        await Response.WriteAsync($"event: {evt.Name}\ndata: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
