using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Application.Services;
using UberEatsWallet.Infrastructure.Wallet;
using UberEatsWallet.Web.Identity;
using UberEatsWallet.Web.Models;

namespace UberEatsWallet.Web.Controllers;

/// <summary>Cash in (mint), cash out (burn), and the balance/history view for the current actor.</summary>
public sealed class WalletController(ICurrentActorAccessor actors, WalletService wallet) : PageController(actors)
{
    private const int HistoryPageSize = 25;
    private const string ToastKey = "toast";

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (Actor is not { } actor)
        {
            return RedirectToLogin();
        }

        var balance = await wallet.GetBalanceAsync(actor.WalletAccountId, ct);
        var (history, historyAvailable) = await TryGetHistoryAsync(actor.WalletAccountId, ct);
        return View(new WalletPageViewModel(actor, balance, history, historyAvailable));
    }

    // History is best-effort: if the ledger read fails, still render the balance and actions.
    private async Task<(WalletHistory History, bool Available)> TryGetHistoryAsync(
        Guid accountId, CancellationToken ct)
    {
        try
        {
            return (await wallet.GetHistoryAsync(accountId, 1, HistoryPageSize, ct), true);
        }
        catch (Exception ex) when (ex is WalletGatewayException or HttpRequestException or TaskCanceledException)
        {
            return (new WalletHistory(1, HistoryPageSize, 0, []), false);
        }
    }

    [HttpPost]
    public async Task<IActionResult> TopUp(decimal amount, CancellationToken ct)
    {
        if (Actor is not { } actor)
        {
            return RedirectToLogin();
        }

        if (amount > 0)
        {
            await wallet.TopUpAsync(actor.WalletAccountId, amount, ct);
            TempData[ToastKey] = $"Top-up of {Format(amount)} submitted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(decimal amount, CancellationToken ct)
    {
        if (Actor is not { } actor)
        {
            return RedirectToLogin();
        }

        if (amount > 0)
        {
            await wallet.WithdrawAsync(actor.WalletAccountId, amount, ct);
            TempData[ToastKey] = $"Withdrawal of {Format(amount)} submitted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static string Format(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
