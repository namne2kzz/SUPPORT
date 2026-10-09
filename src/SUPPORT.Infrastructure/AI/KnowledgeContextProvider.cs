using Microsoft.Agents.AI;
using SUPPORT.Application.Common.Interfaces;

namespace SUPPORT.Infrastructure.AI;

/// <summary>
/// Agent Framework context provider that injects already-retrieved knowledge into the agent's instructions.
/// </summary>
/// <remarks>
/// The framework's own <c>TextSearchProvider</c> runs the search itself, inside the agent. NMate searches first,
/// in the handler, so it can decline without calling the model when nothing relevant was found and can record the
/// citations and similarity — so this provider only formats what was found.
/// </remarks>
/// <param name="knowledge">Chunks to ground the answer on.</param>
internal sealed class KnowledgeContextProvider(IReadOnlyList<KnowledgeHit> knowledge) : AIContextProvider(null, null, null)
{
    /// <inheritdoc />
    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new AIContext { Instructions = NMatePrompts.Knowledge(knowledge) });
}
