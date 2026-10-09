namespace SUPPORT.Infrastructure.Settings;

/// <summary>LLM provider settings (section <c>Llm</c>). Defaults target Gemini's OpenAI-compatible endpoint.</summary>
public sealed class LlmSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Llm";

    /// <summary>OpenAI-compatible base URL.</summary>
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    /// <summary>API key — user-secrets or environment only, never appsettings.json.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Short provider name used in <see cref="EmbeddingModelId"/>.</summary>
    public string ProviderName { get; set; } = "gemini";

    /// <summary>Chat model id.</summary>
    public string ChatModel { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>Embedding model id.</summary>
    public string EmbeddingModel { get; set; } = "gemini-embedding-2";

    /// <summary>Embedding size; must equal the schema's <c>vector(N)</c>.</summary>
    public int EmbeddingDimensions { get; set; } = 768;

    /// <summary>Texts embedded per request — fewer requests means less free-tier quota.</summary>
    public int EmbeddingBatchSize { get; set; } = 50;

    /// <summary>Sampling temperature — low, because answers should stick to the documentation.</summary>
    public float Temperature { get; set; } = 0.2f;

    /// <summary>Upper bound on answer length.</summary>
    public int MaxOutputTokens { get; set; } = 600;

    /// <summary>Display names per product, used in the system prompt.</summary>
    public Dictionary<string, string> ProductDisplayNames { get; set; } = new() { ["dashboard"] = "NFlow (quản lý dự án)" };

    /// <summary>Whether an API key is present.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Identifier stored with every embedding; changing model or size changes it and forces a re-embed.</summary>
    public string EmbeddingModelId => $"{ProviderName}/{EmbeddingModel}@{EmbeddingDimensions}";
}
