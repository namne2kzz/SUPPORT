using System.Text;
using System.Text.RegularExpressions;

namespace SUPPORT.Infrastructure.Knowledge;

/// <summary>A chunk before embedding.</summary>
/// <param name="HeadingPath">Breadcrumb of headings, e.g. <c>Đóng sprint › Task chưa xong</c>.</param>
/// <param name="Content">Chunk text.</param>
/// <param name="TokenCount">Approximate tokens.</param>
internal sealed record TextChunk(string HeadingPath, string Content, int TokenCount);

/// <summary>
/// Cuts markdown along its headings: each <c>##</c>/<c>###</c> section becomes one chunk, and only sections
/// longer than <see cref="ChunkingOptions.MaxTokens"/> are split further — by paragraph, with a small overlap so
/// a sentence that straddles the cut is still findable from both sides.
/// </summary>
internal static partial class MarkdownChunker
{
    /// <summary>Separator used in heading breadcrumbs.</summary>
    public const string PathSeparator = " › ";

    /// <summary>Rough chars-per-token for mixed Vietnamese/English prose; precise enough for sizing chunks.</summary>
    private const double CharsPerToken = 3.5;

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex HeadingPattern();

    /// <summary>Chunks a markdown body.</summary>
    /// <param name="body">Markdown without front-matter.</param>
    /// <param name="title">Document title — the breadcrumb for text before the first heading.</param>
    /// <param name="options">Size limits.</param>
    /// <returns>Chunks in document order.</returns>
    public static IReadOnlyList<TextChunk> Chunk(string body, string title, ChunkingOptions options)
    {
        var chunks = new List<TextChunk>();
        foreach (var (path, text) in Sections(body, title))
            chunks.AddRange(SplitSection(path, text, options));
        return chunks;
    }

    /// <summary>Approximate token count of <paramref name="text"/>.</summary>
    /// <param name="text">Any text.</param>
    /// <returns>Estimated tokens (at least 1 for non-empty text).</returns>
    public static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / CharsPerToken);

    private static IEnumerable<(string Path, string Text)> Sections(string body, string title)
    {
        var stack = new List<(int Level, string Text)>();
        var buffer = new StringBuilder();
        var inCodeFence = false;

        foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) inCodeFence = !inCodeFence;

            var match = inCodeFence ? null : HeadingPattern().Match(line);
            if (match is { Success: true })
            {
                if (Flush(buffer) is { } text) yield return (PathOf(stack, title), text);

                var level = match.Groups[1].Value.Length;
                stack.RemoveAll(h => h.Level >= level);
                // A level-1 heading repeats the document title; it starts a section but adds nothing to the path.
                if (level > 1) stack.Add((level, match.Groups[2].Value.Trim()));
                continue;
            }

            // Always '\n', never AppendLine: Environment.NewLine is "\r\n" on Windows and would change how
            // paragraphs split, so the same file would chunk differently on a dev box and on the server.
            buffer.Append(line).Append('\n');
        }

        if (Flush(buffer) is { } last) yield return (PathOf(stack, title), last);
    }

    private static string? Flush(StringBuilder buffer)
    {
        var text = buffer.ToString().Trim();
        buffer.Clear();
        return text.Length == 0 ? null : text;
    }

    private static string PathOf(List<(int Level, string Text)> stack, string title) =>
        stack.Count == 0 ? title : string.Join(PathSeparator, stack.Select(h => h.Text));

    private static IEnumerable<TextChunk> SplitSection(string path, string text, ChunkingOptions options)
    {
        if (EstimateTokens(text) <= options.MaxTokens)
        {
            yield return new TextChunk(path, text, EstimateTokens(text));
            yield break;
        }

        var pieces = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(p => EstimateTokens(p) > options.MaxTokens ? HardSplit(p, options.TargetTokens) : [p])
            .ToList();

        var current = new List<string>();
        foreach (var piece in pieces)
        {
            if (current.Count > 0 && EstimateTokens(Join(current) + "\n\n" + piece) > options.TargetTokens)
            {
                var chunk = Join(current);
                yield return new TextChunk(path, chunk, EstimateTokens(chunk));
                current = Overlap(current, options.OverlapTokens);
            }

            current.Add(piece);
        }

        if (current.Count > 0)
        {
            var chunk = Join(current);
            yield return new TextChunk(path, chunk, EstimateTokens(chunk));
        }
    }

    /// <summary>Keeps the trailing paragraphs that fit in the overlap budget, to seed the next piece.</summary>
    private static List<string> Overlap(List<string> paragraphs, int overlapTokens)
    {
        var kept = new List<string>();
        var tokens = 0;
        for (var i = paragraphs.Count - 1; i >= 0; i--)
        {
            tokens += EstimateTokens(paragraphs[i]);
            if (tokens > overlapTokens) break;
            kept.Insert(0, paragraphs[i]);
        }

        return kept;
    }

    /// <summary>A single paragraph over the limit (a huge table or list) is cut on line breaks, then on length.</summary>
    private static IEnumerable<string> HardSplit(string paragraph, int targetTokens)
    {
        var maxChars = (int)(targetTokens * CharsPerToken);
        var current = new StringBuilder();
        foreach (var line in paragraph.Split('\n'))
        {
            for (var start = 0; start < line.Length; start += maxChars)
            {
                var part = line.Substring(start, Math.Min(maxChars, line.Length - start));
                if (current.Length > 0 && current.Length + part.Length + 1 > maxChars)
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                }

                current.Append(part).Append('\n');
            }
        }

        if (current.Length > 0) yield return current.ToString().Trim();
    }

    private static string Join(List<string> paragraphs) => string.Join("\n\n", paragraphs);
}

/// <summary>Chunk size limits, in approximate tokens.</summary>
/// <param name="MaxTokens">Sections up to this size stay whole.</param>
/// <param name="TargetTokens">Target size of the pieces of a longer section.</param>
/// <param name="OverlapTokens">Tokens repeated between consecutive pieces.</param>
internal sealed record ChunkingOptions(int MaxTokens, int TargetTokens, int OverlapTokens);
