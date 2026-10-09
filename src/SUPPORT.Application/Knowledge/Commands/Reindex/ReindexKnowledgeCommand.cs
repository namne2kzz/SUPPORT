using MediatR;
using SUPPORT.Application.Common.Interfaces;

namespace SUPPORT.Application.Knowledge.Commands.Reindex;

/// <summary>Re-indexes the calling product's knowledge folder (called by CD after syncing the files).</summary>
public sealed record ReindexKnowledgeCommand : IRequest<IngestionResult>;

/// <summary>Handles <see cref="ReindexKnowledgeCommand"/>.</summary>
/// <param name="caller">Calling product.</param>
/// <param name="ingestion">Ingestion pipeline.</param>
internal sealed class ReindexKnowledgeHandler(ICallerContext caller, IKnowledgeIngestion ingestion)
    : IRequestHandler<ReindexKnowledgeCommand, IngestionResult>
{
    /// <inheritdoc />
    public Task<IngestionResult> Handle(ReindexKnowledgeCommand request, CancellationToken cancellationToken) =>
        ingestion.ReindexAsync(caller.Product, "reindex-api", cancellationToken);
}
