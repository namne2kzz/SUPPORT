using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Infrastructure.Settings;

namespace SUPPORT.Infrastructure.AI;

/// <summary>Batches texts into as few provider calls as possible and returns schema-ready, unit-length vectors.</summary>
/// <param name="generator">Embedding generator (Gemini via the OpenAI-compatible API, cached).</param>
/// <param name="settings">LLM settings.</param>
internal sealed class EmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator, IOptions<LlmSettings> settings)
{
    private readonly LlmSettings _settings = settings.Value;

    /// <summary>Identifier stored with each embedding.</summary>
    public string ModelId => _settings.EmbeddingModelId;

    /// <summary>Whether an API key is configured.</summary>
    public bool IsConfigured => _settings.IsConfigured;

    /// <summary>Texts sent per embedding request.</summary>
    public int BatchSize => Math.Max(1, _settings.EmbeddingBatchSize);

    /// <summary>Embeds texts in batches of <see cref="LlmSettings.EmbeddingBatchSize"/>.</summary>
    /// <param name="texts">Texts to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One normalized vector per text, in input order.</returns>
    /// <exception cref="AssistantUnavailableException">The provider refused (quota) or failed.</exception>
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var options = new EmbeddingGenerationOptions { Dimensions = _settings.EmbeddingDimensions };
        var vectors = new List<float[]>(texts.Count);

        foreach (var batch in texts.Chunk(BatchSize))
        {
            GeneratedEmbeddings<Embedding<float>> result;
            try
            {
                result = await generator.GenerateAsync(batch, options, ct);
            }
            catch (ClientResultException ex)
            {
                throw ProviderErrors.Translate(ex);
            }

            if (result.Count != batch.Length)
                throw new AssistantUnavailableException(AssistantErrorCodes.Unavailable,
                    $"Embedding provider returned {result.Count} vectors for {batch.Length} texts.");

            vectors.AddRange(result.Select(e => Fit(e.Vector.Span, _settings.EmbeddingDimensions)));
        }

        return vectors;
    }

    /// <summary>Embeds one question.</summary>
    /// <param name="text">Question text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Normalized vector.</returns>
    public async Task<float[]> EmbedOneAsync(string text, CancellationToken ct) => (await EmbedAsync([text], ct))[0];

    /// <summary>
    /// Truncates to <paramref name="dimensions"/> and rescales to unit length. Gemini embeddings are Matryoshka-trained,
    /// so a prefix is itself a valid (if slightly coarser) embedding — this also protects the schema if a provider
    /// ignores the requested dimension and returns the full 3072.
    /// </summary>
    /// <param name="vector">Raw vector from the provider.</param>
    /// <param name="dimensions">Schema dimension.</param>
    /// <returns>Unit-length vector of exactly <paramref name="dimensions"/> values.</returns>
    /// <exception cref="InvalidOperationException">The vector is shorter than the schema or all zeros.</exception>
    internal static float[] Fit(ReadOnlySpan<float> vector, int dimensions)
    {
        if (vector.Length < dimensions)
            throw new InvalidOperationException($"Embedding has {vector.Length} dimensions; the schema needs {dimensions}.");

        var fitted = vector[..dimensions].ToArray();
        var norm = Math.Sqrt(fitted.Sum(v => (double)v * v));
        if (norm == 0) throw new InvalidOperationException("Embedding is all zeros.");

        for (var i = 0; i < fitted.Length; i++) fitted[i] = (float)(fitted[i] / norm);
        return fitted;
    }
}
