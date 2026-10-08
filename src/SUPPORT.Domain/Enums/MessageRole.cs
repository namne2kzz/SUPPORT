namespace SUPPORT.Domain.Enums;

/// <summary>Who authored a conversation message.</summary>
public enum MessageRole : short
{
    /// <summary>The end user asking a question.</summary>
    User = 1,

    /// <summary>NMate's answer.</summary>
    Assistant = 2,
}
