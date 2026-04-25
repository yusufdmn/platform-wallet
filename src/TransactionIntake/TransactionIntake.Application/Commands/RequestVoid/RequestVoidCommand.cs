using MediatR;

namespace PlatformWallet.TransactionIntake.Application.Commands.RequestVoid;

public sealed record RequestVoidCommand(Guid CorrelationId) : IRequest;
