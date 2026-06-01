using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Web.Identity;
using UberEatsWallet.Web.Models;

namespace UberEatsWallet.Web.Controllers;

/// <summary>The "act as" login selector and session sign-out.</summary>
public sealed class HomeController(ICurrentActorAccessor actors, ICatalogRepository catalog) : PageController(actors)
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (Actor is { } actor)
        {
            return RedirectHome(actor);
        }

        var customers = await catalog.GetCustomersAsync(ct);
        var restaurants = await catalog.GetRestaurantsAsync(ct);
        return View(new HomeViewModel(customers, restaurants));
    }

    [HttpPost]
    public async Task<IActionResult> SignInAsCustomer(Guid id, CancellationToken ct)
    {
        var customer = await catalog.GetCustomerAsync(id, ct);
        if (customer is null)
        {
            return RedirectToAction(nameof(Index));
        }

        Actors.SignIn(new CurrentActor(ActorType.Customer, customer.Id, customer.Name, customer.WalletAccountId));
        return RedirectToAction("Index", "Restaurants");
    }

    [HttpPost]
    public async Task<IActionResult> SignInAsRestaurant(Guid id, CancellationToken ct)
    {
        var restaurant = await catalog.GetRestaurantAsync(id, ct);
        if (restaurant is null)
        {
            return RedirectToAction(nameof(Index));
        }

        Actors.SignIn(new CurrentActor(ActorType.Restaurant, restaurant.Id, restaurant.Name, restaurant.WalletAccountId));
        return RedirectToAction("Index", "RestaurantDashboard");
    }

    [HttpPost]
    public IActionResult Logout()
    {
        Actors.SignOut();
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    private RedirectToActionResult RedirectHome(CurrentActor actor) =>
        actor.IsCustomer
            ? RedirectToAction("Index", "Restaurants")
            : RedirectToAction("Index", "RestaurantDashboard");
}
