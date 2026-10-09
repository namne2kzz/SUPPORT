namespace SUPPORT.Application.Common.Interfaces;

/// <summary>The LLM side of NMate: turns a question plus retrieved knowledge into a streamed answer.</summary>
public interface ISupportAssistant
{
    /// <summary>Whether a chat model is configured (API key present). Checked before any call.</summary>
    bool IsConfigured { get; }

    /// <summary>Identifier of the chat model, recorded on every answer.</summary>
    string ChatModel { get; }

    /// <summary>Streams an answer grounded on <see cref="AssistantRequest.Knowledge"/>.</summary>
    /// <param name="request">Question, history and knowledge.</param>
    /// <param name="ct">Cancellation token; cancelling stops generation at the provider.</param>
    /// <returns>Text deltas, plus a final update carrying token usage when the provider reports it.</returns>
    /// <exception cref="Exceptions.AssistantUnavailableException">Provider rejected or failed the request.</exception>
    IAsyncEnumerable<AssistantUpdate> StreamAnswerAsync(AssistantRequest request, CancellationToken ct);
}

/// <summary>Input for one answer.</summary>
/// <param name="Product">Product the user is asking about.</param>
/// <param name="Question">The new question.</param>
/// <param name="History">Earlier turns of the conversation, oldest first.</param>
/// <param name="Knowledge">Retrieved chunks to ground the answer on.</param>
/// <param name="Route">Screen the user is on, if known.</param>
public sealed record AssistantRequest(
    string Product,
    string Question,
    IReadOnlyList<ChatTurn> History,
    IReadOnlyList<KnowledgeHit> Knowledge,
    string? Route);

/// <summary>One earlier turn of a conversation.</summary>
/// <param name="IsUser">True for the user's question, false for NMate's answer.</param>
/// <param name="Content">Turn text.</param>
public sealed record ChatTurn(bool IsUser, string Content);

/// <summary>A piece of a streamed answer.</summary>
/// <param name="Text">Text delta, if any.</param>
/// <param name="PromptTokens">Prompt tokens, when the provider reports usage.</param>
/// <param name="CompletionTokens">Completion tokens, when the provider reports usage.</param>
public sealed record AssistantUpdate(string? Text, int? PromptTokens = null, int? CompletionTokens = null);
