namespace PlatformWallet.TransactionIntake.Application;

/// <summary>
/// Assembly marker. Hosts MediatR command handlers (CreateHoldCommand,
/// CreateMintCommand, CreateBurnCommand, RequestCaptureCommand,
/// RequestVoidCommand) plus the inbox consumers from TheMainPlan.md §3.3.2.
/// </summary>
public interface IAssemblyMarker;
