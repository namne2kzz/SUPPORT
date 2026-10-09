using SUPPORT.Domain.Enums;

namespace SUPPORT.Infrastructure.Knowledge;

/// <summary>A parsed knowledge source file: front-matter metadata plus the markdown body.</summary>
/// <param name="SourceKey">Stable key (front-matter <c>key</c>, else the relative path without extension).</param>
/// <param name="Title">Display title.</param>
/// <param name="Module">Product module.</param>
/// <param name="SourceType">Kind of document.</param>
/// <param name="Language">Content language.</param>
/// <param name="Route">Front-end route the document describes.</param>
/// <param name="Suggestions">Starter questions.</param>
/// <param name="Body">Markdown after the front-matter.</param>
/// <param name="ContentHash">SHA-256 (hex) of the whole file with line endings normalized, front-matter included.</param>
internal sealed record KnowledgeFile(
    string SourceKey,
    string Title,
    string Module,
    KnowledgeSourceType SourceType,
    string Language,
    string? Route,
    IReadOnlyList<string> Suggestions,
    string Body,
    string ContentHash);
