using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;

namespace SUPPORT.IntegrationTests.Fakes;

/// <summary>
/// Deterministic stand-in for Gemini embeddings: each word (lower-cased, diacritics removed) is hashed into one
/// dimension. Texts that share words end up close in cosine space, which is enough to test retrieval end to end.
/// </summary>
public sealed class BagOfWordsEmbeddingGenerator(int dimensions = 768) : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>Number of texts embedded so far (to assert caching / skipping).</summary>
    public int EmbeddedTexts { get; private set; }

    /// <summary>Number of provider requests made so far (what the free tier counts).</summary>
    public int Requests { get; private set; }

    /// <summary>When set, the request with this 1-based number fails as if the quota ran out.</summary>
    public int? FailOnRequest { get; set; }

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests++;
        if (Requests == FailOnRequest)
            throw new SUPPORT.Application.Common.Exceptions.AssistantUnavailableException(
                SUPPORT.Application.Common.Exceptions.AssistantErrorCodes.QuotaExceeded, "quota");

        var embeddings = values.ToList().Select(v =>
        {
            EmbeddedTexts++;
            var vector = new float[dimensions];
            foreach (var word in Words(v)) vector[(int)(Fnv1a(word) % (uint)dimensions)] += 1f;
            if (vector.All(x => x == 0)) vector[0] = 1f;
            return new Embedding<float>(vector);
        });

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public void Dispose() { }

    private static IEnumerable<string> Words(string text)
    {
        var plain = new string(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray()).Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();

        return plain.Split(c => !char.IsLetterOrDigit(c)).Where(w => w.Length > 1);
    }

    private static uint Fnv1a(string word)
    {
        var hash = 2166136261;
        foreach (var c in word) hash = (hash ^ c) * 16777619;
        return hash;
    }
}

file static class SplitExtensions
{
    public static string[] Split(this string text, Func<char, bool> isSeparator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        foreach (var c in text)
        {
            if (isSeparator(c))
            {
                if (current.Length > 0) parts.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }

        if (current.Length > 0) parts.Add(current.ToString());
        return [.. parts];
    }
}
