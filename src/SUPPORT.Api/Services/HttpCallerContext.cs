using SUPPORT.Api.Middleware;
using SUPPORT.Application.Common.Interfaces;

namespace SUPPORT.Api.Services;

/// <summary>
/// Reads the caller from the request: product from <see cref="InternalClientAuthMiddleware"/>, user and org from the
/// <c>X-User-Id</c> / <c>X-Org-Id</c> headers the product forwards after authenticating its own user.
/// </summary>
/// <param name="accessor">HTTP context accessor.</param>
public sealed class HttpCallerContext(IHttpContextAccessor accessor) : ICallerContext
{
    /// <summary>Header carrying the end user id.</summary>
    public const string UserHeader = "X-User-Id";

    /// <summary>Header carrying the end user's organization id.</summary>
    public const string OrgHeader = "X-Org-Id";

    private HttpContext Context => accessor.HttpContext
        ?? throw new InvalidOperationException("No HTTP request in progress.");

    /// <inheritdoc />
    public string Product => Context.Items[InternalClientAuthMiddleware.ProductItemKey] as string
        ?? throw new InvalidOperationException("Request was not authenticated as an internal client.");

    /// <inheritdoc />
    public Guid UserId => ReadGuid(UserHeader);

    /// <inheritdoc />
    public Guid OrgId => ReadGuid(OrgHeader);

    private Guid ReadGuid(string header) =>
        Guid.TryParse(Context.Request.Headers[header].ToString(), out var id) && id != Guid.Empty
            ? id
            : throw new MissingCallerIdentityException($"Header {header} is missing or not a valid id.");
}

/// <summary>The request needed a user identity the caller did not forward. Mapped to HTTP 400.</summary>
/// <param name="message">Reason.</param>
public sealed class MissingCallerIdentityException(string message) : Exception(message);
