using decorativeplant_be.Application.Features.AiChat.Commands;
using FluentValidation;

namespace decorativeplant_be.Application.Features.AiChat.Validators;

public sealed class SendAiChatMessageCommandValidator : AbstractValidator<SendAiChatMessageCommand>
{
    private const int MaxMessageLen = 4000;
    private const int MaxTurns = 24;
    private const int MaxImageBase64Chars = 6_000_000;

    public SendAiChatMessageCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Messages)
            .NotEmpty()
            .Must(m => m.Count <= MaxTurns)
            .WithMessage($"At most {MaxTurns} messages are allowed.");

        RuleFor(x => x).Must(ValidateMessages).WithMessage(
            "Each message must have role user or assistant, content at most 4000 characters, and the last user message must have text or an attached image.");

        RuleFor(x => x.AttachedImageBase64)
            .Must(s => s == null || s.Length <= MaxImageBase64Chars)
            .WithMessage("Attached image is too large.")
            .When(x => !string.IsNullOrEmpty(x.AttachedImageBase64));
    }

    private static bool ValidateMessages(SendAiChatMessageCommand cmd)
    {
        var msgs = cmd.Messages;
        for (var i = 0; i < msgs.Count; i++)
        {
            var m = msgs[i];
            if (string.IsNullOrWhiteSpace(m.Role))
            {
                return false;
            }

            if (!string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (m.Content != null && m.Content.Length > MaxMessageLen)
            {
                return false;
            }

            var isLast = i == msgs.Count - 1;
            var isLastUser = isLast && string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase);
            var hasImage = !string.IsNullOrWhiteSpace(cmd.AttachedImageBase64);
            if (string.IsNullOrWhiteSpace(m.Content) && !(isLastUser && hasImage))
            {
                return false;
            }
        }

        if (msgs.Count == 0)
        {
            return false;
        }

        if (!string.Equals(msgs[^1].Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
