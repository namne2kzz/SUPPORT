namespace SUPPORT.Domain.Enums;

/// <summary>Lifecycle of a knowledge document.</summary>
public enum KnowledgeDocumentStatus : short
{
    /// <summary>Searchable.</summary>
    Active = 1,

    /// <summary>Source file was removed; kept so old citations still resolve, but never retrieved.</summary>
    Archived = 2,
}
