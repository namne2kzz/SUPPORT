using FluentValidation;
using MediatR;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Domain.Entities;
using SUPPORT.Domain.Enums;

namespace SUPPORT.Application.Feedback.Commands.SubmitFeedback;

/// <summary>Rates one of NMate's answers in the caller's own conversation. Resubmitting overwrites.</summary>
/// <param name="MessageId">Answer to rate.</param>
/// <param name="Rating">1 helpful, -1 not helpful.</param>
/// <param name="Comment">Optional comment.</param>
public sealed record SubmitFeedbackCommand(Guid MessageId, short Rating, string? Comment) : IRequest;

/// <summary>Validates <see cref="SubmitFeedbackCommand"/>.</summary>
internal sealed class SubmitFeedbackValidator : AbstractValidator<SubmitFeedbackCommand>
{
    /// <summary>Rating ±1 and a bounded comment.</summary>
    public SubmitFeedbackValidator()
    {
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.Rating).Must(r => r is 1 or -1).WithMessage("Rating must be 1 or -1.");
        RuleFor(c => c.Comment).MaximumLength(1000);
    }
}

/// <summary>Handles <see cref="SubmitFeedbackCommand"/>.</summary>
/// <param name="caller">Calling product and user.</param>
/// <param name="conversations">Conversation persistence.</param>
internal sealed class SubmitFeedbackHandler(ICallerContext caller, IConversationRepository conversations)
    : IRequestHandler<SubmitFeedbackCommand>
{
    /// <inheritdoc />
    public async Task Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
    {
        var found = await conversations.GetMessageAsync(request.MessageId, cancellationToken);
        if (found is not { } pair
            || pair.Message.Role != MessageRole.Assistant
            || !pair.Conversation.IsOwnedBy(caller.Product, caller.OrgId, caller.UserId))
            throw new NotFoundException("Message not found.");

        var existing = await conversations.GetFeedbackAsync(request.MessageId, cancellationToken);
        if (existing is null)
            conversations.Add(MessageFeedback.Create(request.MessageId, caller.UserId, request.Rating, request.Comment));
        else
            existing.Update(request.Rating, request.Comment);

        await conversations.SaveChangesAsync(cancellationToken);
    }
}
