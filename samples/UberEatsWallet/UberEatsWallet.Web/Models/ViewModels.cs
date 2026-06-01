using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Domain;
using UberEatsWallet.Web.Identity;

namespace UberEatsWallet.Web.Models;

public sealed record HomeViewModel(IReadOnlyList<Customer> Customers, IReadOnlyList<Restaurant> Restaurants);

public sealed record WalletPageViewModel(
    CurrentActor Actor,
    WalletBalance? Balance,
    WalletHistory History,
    bool HistoryAvailable);

public sealed record MenuPageViewModel(Restaurant Restaurant, IReadOnlyList<MenuItem> Items);

/// <summary>An order plus the display name of the other party (restaurant for a customer, customer for a restaurant).</summary>
public sealed record OrderRow(Order Order, string CounterpartyName);

public sealed record OrdersPageViewModel(IReadOnlyList<OrderRow> Orders);
