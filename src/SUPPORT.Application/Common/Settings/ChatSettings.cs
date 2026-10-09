namespace SUPPORT.Application.Common.Settings;

/// <summary>Conversation and retrieval knobs (section <c>Chat</c>).</summary>
public sealed class ChatSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Chat";

    /// <summary>Earlier messages sent to the model as context.</summary>
    public int HistoryWindow { get; set; } = 10;

    /// <summary>Longest accepted question.</summary>
    public int MaxQuestionLength { get; set; } = 1000;

    /// <summary>Chunks given to the model per answer.</summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Below this best cosine similarity — and with no full-text hit — NMate answers "no documentation"
    /// without calling the model at all. Tune against the eval set.
    /// </summary>
    /// <remarks>
    /// Gemini embeddings sit on a high baseline: first real run (2026-10-09) scored on-topic questions ≈ 0.80 and an
    /// unrelated one ("thời tiết Hà Nội") ≈ 0.50, so the original 0.45 let off-topic questions through to the model.
    /// </remarks>
    public float MinCosineSimilarity { get; set; } = 0.60f;

    /// <summary>Answer used when retrieval found nothing relevant.</summary>
    public string NoAnswerText { get; set; } =
        "Mình chưa có tài liệu về vấn đề này nên không dám trả lời bừa. Bạn thử hỏi admin của tổ chức nhé.";

    /// <summary>Suggestions returned per screen.</summary>
    public int SuggestionCount { get; set; } = 4;
}
