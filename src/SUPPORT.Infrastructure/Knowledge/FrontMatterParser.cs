using System.Security.Cryptography;
using System.Text;
using SUPPORT.Domain.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SUPPORT.Infrastructure.Knowledge;

/// <summary>Splits a knowledge markdown file into YAML front-matter and body, and validates the metadata.</summary>
internal static class FrontMatterParser
{
    private const string Fence = "---";

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Parses one file.</summary>
    /// <param name="relativePath">Path relative to the knowledge root, used as the fallback key.</param>
    /// <param name="raw">Whole file content.</param>
    /// <returns>The parsed file.</returns>
    /// <exception cref="FormatException">Front-matter is missing, malformed, or lacks <c>title</c>/<c>module</c>.</exception>
    public static KnowledgeFile Parse(string relativePath, string raw)
    {
        // ReplaceLineEndings handles CRLF (Windows / git autocrlf), LF and stray CR alike.
        var text = raw.ReplaceLineEndings("\n").TrimStart('﻿');
        if (!text.StartsWith(Fence + "\n", StringComparison.Ordinal))
            throw new FormatException("File must start with a '---' front-matter block.");

        var end = text.IndexOf("\n" + Fence, Fence.Length, StringComparison.Ordinal);
        if (end < 0) throw new FormatException("Front-matter block is not closed with '---'.");

        var yaml = text[(Fence.Length + 1)..end];
        var body = text[(end + Fence.Length + 1)..].TrimStart('\n');

        FrontMatter meta;
        try
        {
            meta = Yaml.Deserialize<FrontMatter?>(yaml) ?? new FrontMatter();
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new FormatException($"Invalid front-matter YAML: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(meta.Title)) throw new FormatException("Front-matter 'title' is required.");
        if (string.IsNullOrWhiteSpace(meta.Module)) throw new FormatException("Front-matter 'module' is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new FormatException("File has no content after the front-matter.");

        var sourceType = KnowledgeSourceType.UserGuide;
        if (!string.IsNullOrWhiteSpace(meta.Type) && !Enum.TryParse(meta.Type, ignoreCase: true, out sourceType))
            throw new FormatException($"Unknown type '{meta.Type}'. Use one of: {string.Join(", ", Enum.GetNames<KnowledgeSourceType>())}.");

        return new KnowledgeFile(
            SourceKey: string.IsNullOrWhiteSpace(meta.Key) ? DefaultKey(relativePath) : meta.Key.Trim(),
            Title: meta.Title.Trim(),
            Module: meta.Module.Trim(),
            SourceType: sourceType,
            Language: string.IsNullOrWhiteSpace(meta.Language) ? "vi" : meta.Language.Trim(),
            Route: string.IsNullOrWhiteSpace(meta.Route) ? null : meta.Route.Trim(),
            Suggestions: meta.Suggestions ?? [],
            Body: body,
            // Hash the normalized text, not the raw bytes: a git checkout flipping LF/CRLF must not count as
            // a content change and re-embed every file (that would spend free-tier quota for nothing).
            ContentHash: Hash(text));
    }

    /// <summary><c>guides\Sprints.md</c> → <c>guides/sprints</c>.</summary>
    private static string DefaultKey(string relativePath) =>
        Path.ChangeExtension(relativePath, null)!.Replace('\\', '/').ToLowerInvariant();

    private static string Hash(string raw) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    /// <summary>YAML shape of the front-matter.</summary>
    private sealed class FrontMatter
    {
        public string? Key { get; set; }
        public string? Title { get; set; }
        public string? Module { get; set; }
        public string? Type { get; set; }
        public string? Route { get; set; }
        public string? Language { get; set; }
        public List<string>? Suggestions { get; set; }
    }
}
