using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Web.Identity;

namespace UberEatsWallet.Web.Controllers;

/// <summary>Base for the session-aware UI controllers. Exposes the current actor and a login redirect.</summary>
public abstract class PageController(ICurrentActorAccessor actors) : Controller
{
    protected ICurrentActorAccessor Actors { get; } = actors;

    protected CurrentActor? Actor => Actors.Current;

    protected IActionResult RedirectToLogin() => RedirectToAction("Index", "Home");
}
