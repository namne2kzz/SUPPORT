using System.ClientModel.Primitives;

namespace SUPPORT.Infrastructure.AI;

/// <summary>
/// Retries only the provider failures that clear on their own within seconds.
/// </summary>
/// <remarks>
/// Gemini answers a demand spike with <c>503 UNAVAILABLE</c> — "This model is currently experiencing
/// high demand. Spikes in demand are usually temporary. Please try again later." — and a second
/// attempt a moment later normally succeeds. With retries switched off entirely, every one of those
/// spikes surfaced to the user as "NMate đang gặp sự cố" after a ~8 second wait.
/// <para>
/// 429 is deliberately not retried: on a free tier the quota window is minutes, not seconds, so
/// retrying only makes the person wait longer before the same refusal. Neither is any 4xx, which
/// says the request itself is wrong and will be wrong again.
/// </para>
/// <para>
/// The delays stay short because somebody is watching a blank chat panel: two extra attempts add at
/// most about three seconds before the widget gives up and says so.
/// </para>
/// </remarks>
internal sealed class TransientOverloadRetryPolicy() : ClientRetryPolicy(maxRetries: 2)
{
    /// <summary>Statuses that mean "the provider is busy", as opposed to "your request is wrong".</summary>
    private static readonly int[] RetryableStatuses = [502, 503, 504];

    /// <inheritdoc />
    protected override bool ShouldRetry(PipelineMessage message, Exception? exception)
    {
        // A transport failure never reached the provider, so the SDK's own judgement applies.
        if (exception is not null) return base.ShouldRetry(message, exception);

        var status = message.Response?.Status;
        return status is not null && Array.IndexOf(RetryableStatuses, status.Value) >= 0;
    }

    /// <inheritdoc />
    protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount) =>
        // 1s then 2s. Long enough for a spike to pass, short enough that the person is not left
        // staring at an empty panel wondering whether anything is happening.
        TimeSpan.FromSeconds(Math.Pow(2, tryCount - 1));
}
