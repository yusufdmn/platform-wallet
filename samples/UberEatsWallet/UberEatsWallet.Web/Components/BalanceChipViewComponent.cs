using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Application.Services;
using UberEatsWallet.Infrastructure.Wallet;
using UberEatsWallet.Web.Identity;

namespace UberEatsWallet.Web.Components;

/// <summary>Renders the current actor's live balance in the nav, degrading gracefully if the wallet is unreachable.</summary>
public sealed class BalanceChipViewComponent(ICurrentActorAccessor actors, WalletService wallet) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var actor = actors.Current;
        if (actor is null)
        {
            return View((WalletBalance?)null);
        }

        try
        {
            var balance = await wallet.GetBalanceAsync(actor.WalletAccountId, HttpContext.RequestAborted);
            return View(balance);
        }
        catch (Exception ex) when (ex is WalletGatewayException or HttpRequestException or TaskCanceledException)
        {
            return View((WalletBalance?)null);
        }
    }
}
