using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Application.Services;
using UberEatsWallet.Web.Identity;
using UberEatsWallet.Web.Models;

namespace UberEatsWallet.Web.Controllers;

/// <summary>Customer-facing: browse restaurants, view a menu, and place an order (hold).</summary>
public sealed class RestaurantsController(
    ICurrentActorAccessor actors,
    ICatalogRepository catalog,
    OrderService orders) : PageController(actors)
{
    private const string ToastKey = "toast";

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (Actor is not { IsCustomer: true })
        {
            return RedirectToLogin();
        }

        var restaurants = await catalog.GetRestaurantsAsync(ct);
        return View(restaurants);
    }

    public async Task<IActionResult> Menu(Guid id, CancellationToken ct)
    {
        if (Actor is not { IsCustomer: true })
        {
            return RedirectToLogin();
        }

        var restaurant = await catalog.GetRestaurantAsync(id, ct);
        if (restaurant is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var items = await catalog.GetMenuAsync(id, ct);
        return View(new MenuPageViewModel(restaurant, items));
    }

    [HttpPost]
    public async Task<IActionResult> Place(Guid menuItemId, int quantity, CancellationToken ct)
    {
        if (Actor is not { IsCustomer: true } actor)
        {
            return RedirectToLogin();
        }

        var safeQuantity = quantity < 1 ? 1 : quantity;
        await orders.PlaceOrderAsync(actor.Id, menuItemId, safeQuantity, ct);
        TempData[ToastKey] = "Order placed — funds are held until the restaurant responds.";
        return RedirectToAction("Index", "Orders");
    }
}
