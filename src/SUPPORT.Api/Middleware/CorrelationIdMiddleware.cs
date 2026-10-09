using Serilog.Context;

namespace SUPPORT.Api.Middleware;

/// <summary>
/// Takes the <c>X-Correlation-Id</c> forwarded by DASHBOARD (or generates one), echoes it on the response and pushes
/// it into the Serilog context, so one question can be followed across DASHBOARD and NMate logs.
/// </summary>
/// <param name="next">Next middleware.</param>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>Header carrying the correlation id.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Resolves the id and runs the rest of the pipeline inside its logging scope.</summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].FirstOrDefault();
        // Attacker-controlled and written to logs: cap the length and allow only id-like characters.
        var correlationId = !string.IsNullOrWhiteSpace(inbound) && inbound.Length <= 64 && inbound.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':')
            ? inbound
            : context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
            await next(context);
    }
}
