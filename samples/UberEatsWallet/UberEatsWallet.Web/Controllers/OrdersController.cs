using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Application.Services;
using UberEatsWallet.Web.Identity;
using UberEatsWallet.Web.Models;

namespace UberEatsWallet.Web.Controllers;

/// <summary>Customer-facing: track order status (live via htmx) and cancel while still pending.</summary>
public sealed class OrdersController(
    ICurrentActorAccessor actors,
    IOrderRepository orders,
    ICatalogRepository catalog,
    OrderService orderService) : PageController(actors)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (Actor is not { IsCustomer: true } actor)
        {
            return RedirectToLogin();
        }

        return View(await BuildAsync(actor.Id, ct));
    }

    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (Actor is not { IsCustomer: true } actor)
        {
            return RedirectToLogin();
        }

        return PartialView("_OrderList", await BuildAsync(actor.Id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (Actor is not { IsCustomer: true })
        {
            return RedirectToLogin();
        }

        await orderService.CancelAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    private async Task<OrdersPageViewModel> BuildAsync(Guid customerId, CancellationToken ct)
    {
        var list = await orders.GetForCustomerAsync(customerId, ct);
        var restaurantNames = (await catalog.GetRestaurantsAsync(ct)).ToDictionary(r => r.Id, r => r.Name);
        var rows = list
            .Select(o => new OrderRow(o, restaurantNames.GetValueOrDefault(o.RestaurantId, "Unknown")))
            .ToList();
        return new OrdersPageViewModel(rows);
    }
}
