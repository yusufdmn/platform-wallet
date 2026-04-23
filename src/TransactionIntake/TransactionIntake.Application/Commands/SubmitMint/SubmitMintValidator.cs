using FluentValidation;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;

public sealed class SubmitMintValidator : AbstractValidator<SubmitMintCommand>
{
    private const int MaxAssetLength        = 16;
    private const int MaxIdempotencyKeyLength = 128;

    public SubmitMintValidator()
    {
        RuleFor(x => x.CreditAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Asset)
            .NotEmpty()
            .MaximumLength(MaxAssetLength);
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(MaxIdempotencyKeyLength);
    }
}
