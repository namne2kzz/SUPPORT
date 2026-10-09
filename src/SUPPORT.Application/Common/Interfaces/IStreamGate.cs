namespace SUPPORT.Application.Common.Interfaces;

/// <summary>Limits each user to one answer being generated at a time (protects the free-tier quota).</summary>
public interface IStreamGate
{
    /// <summary>Tries to claim the single stream slot for <paramref name="key"/>.</summary>
    /// <param name="key">Per-user key.</param>
    /// <returns>A lease to dispose when the stream ends, or null when another stream is already running.</returns>
    IDisposable? TryAcquire(string key);
}
