using MediatR;
using Microsoft.AspNetCore.Mvc;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Application.Knowledge.Commands.Reindex;
using SUPPORT.Application.Knowledge.Queries.GetStatus;
using SUPPORT.Application.Knowledge.Queries.GetSuggestions;

namespace SUPPORT.Api.Controllers.Internal;

/// <summary>Knowledge base status, suggestions and re-indexing.</summary>
/// <param name="mediator">Mediator.</param>
[ApiController]
[Route("internal/v1")]
public sealed class KnowledgeController(IMediator mediator) : ControllerBase
{
    /// <summary>Starter questions for the screen the user is on.</summary>
    /// <param name="route">Current front-end route.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Suggested questions.</returns>
    [HttpGet("suggestions")]
    public Task<IReadOnlyList<string>> Suggestions([FromQuery] string? route, CancellationToken ct) =>
        mediator.Send(new GetSuggestionsQuery(route), ct);

    /// <summary>Whether NMate can answer right now. Never calls the LLM.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Availability and knowledge base size.</returns>
    [HttpGet("status")]
    public Task<StatusDto> Status(CancellationToken ct) => mediator.Send(new GetStatusQuery(), ct);

    /// <summary>Re-indexes the calling product's knowledge folder (CD calls this after syncing the files).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Counts for the run.</returns>
    [HttpPost("knowledge/reindex")]
    public Task<IngestionResult> Reindex(CancellationToken ct) => mediator.Send(new ReindexKnowledgeCommand(), ct);
}
