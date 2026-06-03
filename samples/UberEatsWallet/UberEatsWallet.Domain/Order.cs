namespace UberEatsWallet.Domain;

/// <summary>
/// An order placed by a customer against a restaurant, backed by a wallet transfer.
/// <para>
/// State transitions are deliberately <b>tolerant</b>: each only fires from its valid
/// source state and is otherwise a no-op. Wallet webhooks are at-least-once, so the same
/// event may arrive twice — a tolerant transition makes re-delivery harmless.
/// </para>
/// </summary>
public sealed class Order
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid MenuItemId { get; private set; }

    /// <summary>Snapshot of the item name/price at order time, so history is stable if the menu changes.</summary>
    public string ItemName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>Correlation id of the order's hold transfer (== the wallet transactionId). Used to capture/void.</summary>
    public Guid OrderTransactionId { get; private set; }

    /// <summary>Correlation id of the reverse (refund) transfer, once a refund is issued.</summary>
    public Guid? RefundTransactionId { get; private set; }

    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Order() { } // EF materialisation

    public static Order Place(
        Guid orderId,
        Guid customerId,
        MenuItem item,
        int quantity,
        Guid orderTransactionId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new Order
        {
            Id = orderId,
            CustomerId = customerId,
            RestaurantId = item.RestaurantId,
            MenuItemId = item.Id,
            ItemName = item.Name,
            UnitPrice = item.Price,
            Quantity = quantity,
            Amount = item.Price * quantity,
            OrderTransactionId = orderTransactionId,
            Status = OrderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Restaurant accepted → hold captured.</summary>
    public void MarkAccepted(DateTimeOffset now) => TransitionFromPending(OrderStatus.Accepted, now);

    /// <summary>Restaurant rejected → hold voided.</summary>
    public void MarkRejected(DateTimeOffset now) => TransitionFromPending(OrderStatus.Rejected, now);

    /// <summary>Customer cancelled before acceptance → hold voided.</summary>
    public void MarkCancelled(DateTimeOffset now) => TransitionFromPending(OrderStatus.Cancelled, now);

    /// <summary>Hold TTL elapsed and the wallet auto-voided (detected via a void webhook while still Pending).</summary>
    public void MarkExpired(DateTimeOffset now) => TransitionFromPending(OrderStatus.Expired, now);

    /// <summary>The hold itself failed (e.g. insufficient funds).</summary>
    public void MarkFailed(DateTimeOffset now) => TransitionFromPending(OrderStatus.Failed, now);

    /// <summary>Reverse an accepted order. Records the refund transfer's correlation id.</summary>
    public void MarkRefunded(Guid refundTransactionId, DateTimeOffset now)
    {
        if (Status != OrderStatus.Accepted)
        {
            return;
        }

        RefundTransactionId = refundTransactionId;
        Status = OrderStatus.Refunded;
        UpdatedAt = now;
    }

    private void TransitionFromPending(OrderStatus target, DateTimeOffset now)
    {
        if (Status != OrderStatus.Pending)
        {
            return;
        }

        Status = target;
        UpdatedAt = now;
    }
}
