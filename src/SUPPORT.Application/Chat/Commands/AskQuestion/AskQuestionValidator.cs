using FluentValidation;
using Microsoft.Extensions.Options;
using SUPPORT.Application.Common.Settings;

namespace SUPPORT.Application.Chat.Commands.AskQuestion;

/// <summary>Validates <see cref="AskQuestionCommand"/>.</summary>
internal sealed class AskQuestionValidator : AbstractValidator<AskQuestionCommand>
{
    /// <summary>Builds the rules from the configured limits.</summary>
    /// <param name="settings">Chat settings.</param>
    public AskQuestionValidator(IOptions<ChatSettings> settings)
    {
        RuleFor(c => c.Message)
            .NotEmpty()
            .Must(m => !string.IsNullOrWhiteSpace(m)).WithMessage("Câu hỏi không được để trống.")
            .MaximumLength(settings.Value.MaxQuestionLength);

        RuleFor(c => c.ConversationId)
            .NotEqual(Guid.Empty).When(c => c.ConversationId.HasValue);

        RuleFor(c => c.Route).MaximumLength(200);
    }
}
