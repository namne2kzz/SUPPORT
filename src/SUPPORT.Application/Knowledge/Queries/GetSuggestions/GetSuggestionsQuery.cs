using MediatR;
using Microsoft.Extensions.Options;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Application.Common.Settings;

namespace SUPPORT.Application.Knowledge.Queries.GetSuggestions;

/// <summary>Starter questions for the screen the user is on. Static (from front-matter) — costs no quota.</summary>
/// <param name="Route">Current front-end route.</param>
public sealed record GetSuggestionsQuery(string? Route) : IRequest<IReadOnlyList<string>>;

/// <summary>Handles <see cref="GetSuggestionsQuery"/>.</summary>
/// <param name="caller">Calling product.</param>
/// <param name="catalog">Knowledge catalog.</param>
/// <param name="settings">Chat settings.</param>
internal sealed class GetSuggestionsHandler(ICallerContext caller, IKnowledgeCatalog catalog, IOptions<ChatSettings> settings)
    : IRequestHandler<GetSuggestionsQuery, IReadOnlyList<string>>
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> Handle(GetSuggestionsQuery request, CancellationToken cancellationToken) =>
        catalog.GetSuggestionsAsync(caller.Product, request.Route, settings.Value.SuggestionCount, cancellationToken);
}
