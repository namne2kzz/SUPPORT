using System.ClientModel;
using SUPPORT.Application.Common.Exceptions;

namespace SUPPORT.Infrastructure.AI;

/// <summary>Maps OpenAI-SDK failures to the application's provider-neutral exception.</summary>
internal static class ProviderErrors
{
    /// <summary>Translates a provider error.</summary>
    /// <param name="ex">Error from the OpenAI SDK.</param>
    /// <returns>The exception to throw.</returns>
    public static AssistantUnavailableException Translate(ClientResultException ex) => ex.Status switch
    {
        429 => new AssistantUnavailableException(AssistantErrorCodes.QuotaExceeded,
            "NMate tạm hết lượt sử dụng, bạn thử lại sau ít phút nhé.", ex),
        401 or 403 => new AssistantUnavailableException(AssistantErrorCodes.Unavailable,
            "NMate chưa được cấu hình đúng API key.", ex),
        _ => new AssistantUnavailableException(AssistantErrorCodes.Unavailable,
            "NMate đang gặp sự cố, bạn thử lại sau nhé.", ex),
    };
}
