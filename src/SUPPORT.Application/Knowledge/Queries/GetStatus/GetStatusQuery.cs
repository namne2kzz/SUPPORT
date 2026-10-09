using MediatR;
using SUPPORT.Application.Common.Interfaces;

namespace SUPPORT.Application.Knowledge.Queries.GetStatus;

/// <summary>Whether NMate can answer right now; the widget uses it to show a maintenance state.</summary>
public sealed record GetStatusQuery : IRequest<StatusDto>;

/// <summary>NMate availability.</summary>
/// <param name="Available">Model configured and at least one document indexed.</param>
/// <param name="ChatModel">Configured chat model.</param>
/// <param name="ActiveDocuments">Searchable documents for the product.</param>
/// <param name="LastIngestionAt">UTC time of the latest finished re-index.</param>
public sealed record StatusDto(bool Available, string ChatModel, int ActiveDocuments, DateTime? LastIngestionAt);

/// <summary>Handles <see cref="GetStatusQuery"/>. Never calls the LLM.</summary>
/// <param name="caller">Calling product.</param>
/// <param name="catalog">Knowledge catalog.</param>
/// <param name="assistant">Assistant (for configuration state only).</param>
internal sealed class GetStatusHandler(ICallerContext caller, IKnowledgeCatalog catalog, ISupportAssistant assistant)
    : IRequestHandler<GetStatusQuery, StatusDto>
{
    /// <inheritdoc />
    public async Task<StatusDto> Handle(GetStatusQuery request, CancellationToken cancellationToken)
    {
        var (documents, lastIngestion) = await catalog.GetStatsAsync(caller.Product, cancellationToken);
        return new StatusDto(assistant.IsConfigured && documents > 0, assistant.ChatModel, documents, lastIngestion);
    }
}
