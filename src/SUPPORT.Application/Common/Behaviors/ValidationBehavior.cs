using FluentValidation;
using MediatR;

namespace SUPPORT.Application.Common.Behaviors;

/// <summary>Runs all FluentValidation validators before a request handler.</summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
/// <param name="validators">Registered validators for the request.</param>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(validators, request, cancellationToken);
        return await next();
    }
}

/// <summary>Same as <see cref="ValidationBehavior{TRequest,TResponse}"/> for streaming requests, which MediatR pipes separately.</summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Streamed item type.</typeparam>
/// <param name="validators">Registered validators for the request.</param>
public sealed class StreamValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(validators, request, cancellationToken);
        await foreach (var item in next().WithCancellation(cancellationToken))
            yield return item;
    }
}

/// <summary>Shared validation routine.</summary>
internal static class RequestValidation
{
    /// <summary>Runs every validator and throws one <see cref="ValidationException"/> with all failures.</summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <param name="validators">Validators to run.</param>
    /// <param name="request">Request to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when validation passes.</returns>
    public static async Task ValidateAsync<TRequest>(IEnumerable<IValidator<TRequest>> validators, TRequest request, CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);
    }
}
