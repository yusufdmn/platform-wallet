using FluentValidation;
using MediatR;

namespace PlatformWallet.TransactionIntake.Application.Behaviours;

public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var failures = await ValidateAsync(request, cancellationToken);

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }

    private async Task<List<FluentValidation.Results.ValidationFailure>> ValidateAsync(
        TRequest          request,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        return results
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();
    }
}
