namespace PlatformWallet.TransactionIntake.Domain;

public enum TransactionStatus { Pending, Held, CaptureRequested, VoidRequested, Captured, Voided, Failed }
