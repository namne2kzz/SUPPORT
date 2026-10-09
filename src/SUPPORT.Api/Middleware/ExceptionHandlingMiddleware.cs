using FluentValidation;
using SUPPORT.Api.Services;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Domain.Common;

namespace SUPPORT.Api.Middleware;

/// <summary>Translates known exceptions into JSON error responses <c>{ error, message }</c>.</summary>
/// <param name="next">Next middleware.</param>
/// <param name="logger">Logger.</param>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Invokes the pipeline, mapping known exceptions to status codes.</summary>
    /// <param name="context">HTTP context.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away (closed the widget mid-answer). Nothing to send and nothing to log as an error.
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, code, message) = Map(ex);
            // A missing key or an exhausted free-tier quota is an expected operating state, not a bug.
            if (ex is AssistantUnavailableException)
                logger.LogWarning("Request refused: {Code} — {Reason}", code, ex.InnerException?.Message ?? ex.Message);
            else if (status >= 500)
                logger.LogError(ex, "Request failed with {Code}", code);

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { error = code, message });
        }
    }

    private static (int Status, string Code, string Message) Map(Exception ex) => ex switch
    {
        ValidationException v => (400, "validation_error", string.Join("; ", v.Errors.Select(e => e.ErrorMessage))),
        MissingCallerIdentityException m => (400, "missing_identity", m.Message),
        NotFoundException n => (404, "not_found", n.Message),
        ConflictException c => (409, c.Code, c.Message),
        DomainException d => (409, "domain_error", d.Message),
        AssistantUnavailableException a => (503, a.Code, a.Message),
        _ => (500, "server_error", "An unexpected error occurred."),
    };
}
