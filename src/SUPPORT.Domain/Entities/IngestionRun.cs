using SUPPORT.Domain.Common;

namespace SUPPORT.Domain.Entities;

/// <summary>Audit record of one knowledge re-index pass for a product.</summary>
public sealed class IngestionRun : Entity
{
    /// <summary>Product that was indexed.</summary>
    public string Product { get; private set; } = default!;

    /// <summary>What started the run: <c>startup</c>, <c>reindex-api</c>.</summary>
    public string Trigger { get; private set; } = default!;

    /// <summary>UTC start time.</summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>UTC end time; null while running or if the process died.</summary>
    public DateTime? FinishedAt { get; private set; }

    /// <summary>Source files found.</summary>
    public int DocsScanned { get; private set; }

    /// <summary>Documents (re)embedded.</summary>
    public int DocsChanged { get; private set; }

    /// <summary>Documents archived because their file disappeared.</summary>
    public int DocsArchived { get; private set; }

    /// <summary>Chunks written.</summary>
    public int ChunksWritten { get; private set; }

    /// <summary>Failure description, if the run did not complete.</summary>
    public string? Error { get; private set; }

    private IngestionRun() { }

    /// <summary>Starts a run.</summary>
    /// <param name="product">Product being indexed.</param>
    /// <param name="trigger">What started the run.</param>
    /// <returns>The running record.</returns>
    public static IngestionRun Start(string product, string trigger) =>
        new() { Product = product, Trigger = trigger, StartedAt = DateTime.UtcNow };

    /// <summary>Records one scanned file.</summary>
    public void CountScanned() => DocsScanned++;

    /// <summary>Records one re-embedded document and its chunk count.</summary>
    /// <param name="chunks">Chunks written for the document.</param>
    public void CountChanged(int chunks)
    {
        DocsChanged++;
        ChunksWritten += chunks;
    }

    /// <summary>Records one archived document.</summary>
    public void CountArchived() => DocsArchived++;

    /// <summary>Marks the run finished, optionally with an error.</summary>
    /// <param name="error">Failure description, or null on success.</param>
    public void Finish(string? error = null)
    {
        FinishedAt = DateTime.UtcNow;
        Error = error;
    }
}
