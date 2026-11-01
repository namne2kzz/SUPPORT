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
            "NMate has used up its quota for now. Please try again in a few minutes.", ex),
        401 or 403 => new AssistantUnavailableException(AssistantErrorCodes.Unavailable,
            "NMate's API key is not configured correctly.", ex),
        // The provider is busy, not broken, and it clears on its own — worth telling the person so
        // they try again instead of reporting a fault. Retries have already been spent by this point
        // (see TransientOverloadRetryPolicy), so reaching here means the spike outlasted them.
        502 or 503 or 504 => new AssistantUnavailableException(AssistantErrorCodes.Unavailable,
            "NMate is overloaded. Please try again shortly.", ex),
        _ => new AssistantUnavailableException(AssistantErrorCodes.Unavailable,
            "NMate ran into a problem. Please try again later.", ex),
    };
}
