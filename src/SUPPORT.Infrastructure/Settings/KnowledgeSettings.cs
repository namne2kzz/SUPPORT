namespace SUPPORT.Infrastructure.Settings;

/// <summary>Where each product's knowledge files live (section <c>Knowledge</c>).</summary>
public sealed class KnowledgeSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Knowledge";

    /// <summary>Product key → folder of markdown files (dev: the DASHBOARD repo checkout; prod: a mounted volume).</summary>
    public Dictionary<string, string> Sources { get; set; } = [];

    /// <summary>Re-index every configured product when the API starts.</summary>
    public bool IngestOnStartup { get; set; } = true;

    /// <summary>Hard upper bound for one chunk, in approximate tokens.</summary>
    public int MaxChunkTokens { get; set; } = 500;

    /// <summary>Target size when a long section has to be split.</summary>
    public int TargetChunkTokens { get; set; } = 400;

    /// <summary>Approximate tokens repeated between consecutive pieces of a split section.</summary>
    public int OverlapTokens { get; set; } = 60;

    /// <summary>Days of inactivity after which a conversation is deleted.</summary>
    public int ConversationRetentionDays { get; set; } = 90;
}
