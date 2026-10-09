using MediatR;
using SUPPORT.Application.Chat.DTOs;

namespace SUPPORT.Application.Chat.Commands.AskQuestion;

/// <summary>Asks NMate a question and streams the answer.</summary>
/// <param name="ConversationId">Existing conversation to continue, or null to start one.</param>
/// <param name="Message">The question.</param>
/// <param name="Route">Front-end route the user is on (boosts that module's documents).</param>
public sealed record AskQuestionCommand(Guid? ConversationId, string Message, string? Route)
    : IStreamRequest<ChatStreamEvent>;
