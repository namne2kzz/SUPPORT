using System.Collections.Concurrent;
using SUPPORT.Application.Common.Interfaces;

namespace SUPPORT.Infrastructure.Services;

/// <summary>
/// Process-local <see cref="IStreamGate"/>. Enough for one API instance; running several would need a shared
/// store (Redis) for the limit to hold across instances.
/// </summary>
internal sealed class InMemoryStreamGate : IStreamGate
{
    private readonly ConcurrentDictionary<string, byte> _active = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IDisposable? TryAcquire(string key) =>
        _active.TryAdd(key, 0) ? new Lease(this, key) : null;

    private sealed class Lease(InMemoryStreamGate owner, string key) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner._active.TryRemove(key, out _);
        }
    }
}
