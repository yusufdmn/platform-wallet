using UberEatsWallet.Domain;

namespace UberEatsWallet.Web.Identity;

/// <summary>Maps an order status to a Tailwind badge style, so views stay declarative.</summary>
public static class OrderStatusStyle
{
    public static string BadgeClasses(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "bg-amber-100 text-amber-800 ring-amber-600/20",
        OrderStatus.Accepted => "bg-emerald-100 text-emerald-800 ring-emerald-600/20",
        OrderStatus.Rejected => "bg-rose-100 text-rose-800 ring-rose-600/20",
        OrderStatus.Cancelled => "bg-slate-100 text-slate-700 ring-slate-500/20",
        OrderStatus.Expired => "bg-slate-100 text-slate-700 ring-slate-500/20",
        OrderStatus.Refunded => "bg-sky-100 text-sky-800 ring-sky-600/20",
        OrderStatus.Failed => "bg-rose-100 text-rose-800 ring-rose-600/20",
        _ => "bg-slate-100 text-slate-700 ring-slate-500/20",
    };

    public static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Awaiting restaurant",
        OrderStatus.Accepted => "Accepted",
        OrderStatus.Rejected => "Rejected",
        OrderStatus.Cancelled => "Cancelled",
        OrderStatus.Expired => "Expired",
        OrderStatus.Refunded => "Refunded",
        OrderStatus.Failed => "Failed",
        _ => status.ToString(),
    };
}
