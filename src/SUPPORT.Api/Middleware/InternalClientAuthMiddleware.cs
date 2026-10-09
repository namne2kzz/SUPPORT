using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SUPPORT.Api.Settings;

namespace SUPPORT.Api.Middleware;

/// <summary>
/// Guards <c>/internal/*</c>: the <c>X-Internal-Token</c> header must match one configured client, and that client's
/// product is stored for the request. The product is therefore decided by the token — a caller cannot claim to be
/// another product by sending a header.
/// </summary>
/// <param name="next">Next middleware.</param>
/// <param name="clients">Configured internal clients.</param>
public sealed class InternalClientAuthMiddleware(RequestDelegate next, IOptions<List<InternalClientSettings>> clients)
{
    /// <summary>Header carrying the shared secret.</summary>
    public const string TokenHeader = "X-Internal-Token";

    /// <summary><see cref="HttpContext.Items"/> key holding the authenticated product.</summary>
    public const string ProductItemKey = "Support.Product";

    /// <summary>Authenticates the caller before forwarding the request.</summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/internal"))
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[TokenHeader].ToString();
        var client = string.IsNullOrEmpty(provided) ? null : Match(provided);
        if (client is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "unauthorized", message = $"Invalid or missing {TokenHeader}." });
            return;
        }

        context.Items[ProductItemKey] = client.Product;
        await next(context);
    }

    private InternalClientSettings? Match(string provided)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        // Every configured token is compared (no early exit) in constant time, so timing reveals nothing.
        InternalClientSettings? match = null;
        foreach (var client in clients.Value.Where(c => !string.IsNullOrWhiteSpace(c.Token) && !string.IsNullOrWhiteSpace(c.Product)))
        {
            if (CryptographicOperations.FixedTimeEquals(providedBytes, Encoding.UTF8.GetBytes(client.Token)))
                match = client;
        }

        return match;
    }
}
