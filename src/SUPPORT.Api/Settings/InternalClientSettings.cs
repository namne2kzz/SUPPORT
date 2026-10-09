namespace SUPPORT.Api.Settings;

/// <summary>One product allowed to call <c>/internal/*</c> (array section <c>InternalClients</c>).</summary>
/// <remarks>Rotate a token by adding a second entry for the same product, switching the caller, then removing the old one.</remarks>
public sealed class InternalClientSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "InternalClients";

    /// <summary>Product key this token authenticates as, e.g. <c>dashboard</c>.</summary>
    public string Product { get; set; } = "";

    /// <summary>Shared secret (≥ 32 characters) — user-secrets or environment only.</summary>
    public string Token { get; set; } = "";
}
