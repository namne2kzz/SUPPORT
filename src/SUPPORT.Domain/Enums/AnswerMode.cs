namespace SUPPORT.Domain.Enums;

/// <summary>How an answer was produced — lets reports separate documentation answers from general chat.</summary>
public enum AnswerMode : short
{
    /// <summary>Grounded on retrieved product documentation; carries citations.</summary>
    Documentation = 1,

    /// <summary>No relevant documentation: answered from the model's general knowledge, without citations.</summary>
    General = 2,

    /// <summary>No relevant documentation and general answers are switched off: declined without calling the model.</summary>
    Declined = 3,
}
