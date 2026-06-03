namespace UberEatsWallet.Domain;

/// <summary>
/// Lifecycle of an order, mirrored from the wallet's transfer saga:
/// Pending = funds held, awaiting the restaurant; the rest are terminal.
/// </summary>
public enum OrderStatus
{
    Pending,    // transfer placed → funds Held in the ledger
    Accepted,   // restaurant accepted → hold captured (customer → restaurant)
    Rejected,   // restaurant rejected → hold voided
    Cancelled,  // customer cancelled before acceptance → hold voided
    Expired,    // 5-min hold TTL elapsed → wallet auto-voided
    Refunded,   // accepted order reversed → restaurant → customer transfer captured
    Failed,     // hold could not be placed (e.g. insufficient funds)
}
