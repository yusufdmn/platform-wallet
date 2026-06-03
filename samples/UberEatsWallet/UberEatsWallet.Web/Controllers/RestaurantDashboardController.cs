using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Application.Services;
using UberEatsWallet.Web.Identity;
using UberEatsWallet.Web.Models;

namespace UberEatsWallet.Web.Controllers;

/// <summary>Restaurant-facing: incoming orders with accept (capture), reject (void), and refund.</summary>
public sealed class RestaurantDashboardController(
    ICurrentActorAccessor actors,
    IOrderRepository orders,
    ICatalogRepository catalog,
    OrderService orderService,
    RefundService refunds) : PageController(actors)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (Actor is not { IsRestaurant: true } actor)
        {
            return RedirectToLogin();
        }

        return View(await BuildAsync(actor.Id, ct));
    }

    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (Actor is not { IsRestaurant: true } actor)
        {
            return RedirectToLogin();
        }

        return PartialView("_IncomingList", await BuildAsync(actor.Id, ct));
    }

    [HttpPost]
    public Task<IActionResult> Accept(Guid id, CancellationToken ct) =>
        ResolveAsync(() => orderService.AcceptAsync(id, ct));

    [HttpPost]
    public Task<IActionResult> Reject(Guid id, CancellationToken ct) =>
        ResolveAsync(() => orderService.RejectAsync(id, ct));

    [HttpPost]
    public Task<IActionResult> Refund(Guid id, CancellationToken ct) =>
        ResolveAsync(() => refunds.RefundAsync(id, ct));

    private async Task<IActionResult> ResolveAsync(Func<Task> action)
    {
        if (Actor is not { IsRestaurant: true })
        {
            return RedirectToLogin();
        }

        await action();
        return RedirectToAction(nameof(Index));
    }

    private async Task<OrdersPageViewModel> BuildAsync(Guid restaurantId, CancellationToken ct)
    {
        var list = await orders.GetForRestaurantAsync(restaurantId, ct);
        var customerNames = (await catalog.GetCustomersAsync(ct)).ToDictionary(c => c.Id, c => c.Name);
        var rows = list
            .Select(o => new OrderRow(o, customerNames.GetValueOrDefault(o.CustomerId, "Unknown")))
            .ToList();
        return new OrdersPageViewModel(rows);
    }
}
