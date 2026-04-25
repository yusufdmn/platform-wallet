using MediatR;

namespace PlatformWallet.TransactionIntake.Application.Commands.RequestCapture;

public sealed record RequestCaptureCommand(Guid CorrelationId) : IRequest;
