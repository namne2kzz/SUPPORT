namespace SUPPORT.Application.Common.Interfaces;

/// <summary>
/// Who is calling: the product (resolved from the internal token, never from a header) and the end user
/// that product authenticated and forwarded.
/// </summary>
public interface ICallerContext
{
    /// <summary>Calling product key, e.g. <c>dashboard</c>.</summary>
    string Product { get; }

    /// <summary>End user id forwarded by the product. Throws when the request carried none.</summary>
    Guid UserId { get; }

    /// <summary>End user's organization forwarded by the product. Throws when the request carried none.</summary>
    Guid OrgId { get; }
}
