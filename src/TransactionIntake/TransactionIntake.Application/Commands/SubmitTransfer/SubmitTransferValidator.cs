using FluentValidation;

namespace PlatformWallet.TransactionIntake.Application.Commands.SubmitTransfer;

public sealed class SubmitTransferValidator : AbstractValidator<SubmitTransferCommand>
{
    public SubmitTransferValidator()
    {
        RuleFor(x => x.DebitAccountId).NotEmpty();
        RuleFor(x => x.CreditAccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Asset).NotEmpty().MaximumLength(16);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}
