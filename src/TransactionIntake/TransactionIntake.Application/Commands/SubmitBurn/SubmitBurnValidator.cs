using FluentValidation;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitBurn;

public sealed class SubmitBurnValidator : AbstractValidator<SubmitBurnCommand>
{
    private const int MaxAssetLength          = 16;
    private const int MaxIdempotencyKeyLength = 128;

    public SubmitBurnValidator()
    {
        RuleFor(x => x.DebitAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Asset)
            .NotEmpty()
            .MaximumLength(MaxAssetLength);
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(MaxIdempotencyKeyLength);
    }
}
