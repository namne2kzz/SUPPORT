namespace SUPPORT.Domain.Enums;

/// <summary>What kind of document a knowledge file is — drives nothing yet but lets reports group answers by source.</summary>
public enum KnowledgeSourceType : short
{
    /// <summary>Step-by-step guide for one module.</summary>
    UserGuide = 1,

    /// <summary>Frequently asked questions.</summary>
    Faq = 2,

    /// <summary>What changed in a release.</summary>
    ReleaseNote = 3,

    /// <summary>Definitions of the product's terms.</summary>
    Glossary = 4,
}
