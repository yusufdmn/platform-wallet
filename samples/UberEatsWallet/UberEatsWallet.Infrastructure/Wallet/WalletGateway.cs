using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using UberEatsWallet.Application.Abstractions;

namespace UberEatsWallet.Infrastructure.Wallet;

/// <summary>
/// The Platform Wallet adapter. A typed HttpClient whose base address, <c>api-version</c> header,
/// and bearer token are configured at registration; this class only shapes requests and parses
/// responses. Writes return the wallet <c>transactionId</c> (== saga <c>correlationId</c>).
/// </summary>
internal sealed class WalletGateway(HttpClient http, IOptions<WalletOptions> options) : IWalletGateway
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private readonly string asset = options.Value.Asset;

    public Task<Guid> MintAsync(Guid accountId, decimal amount, string idempotencyKey, CancellationToken ct) =>
        SubmitAsync("/mint", new { creditAccountId = accountId, amount, asset }, idempotencyKey, ct);

    public Task<Guid> BurnAsync(Guid accountId, decimal amount, string idempotencyKey, CancellationToken ct) =>
        SubmitAsync("/burn", new { debitAccountId = accountId, amount, asset }, idempotencyKey, ct);

    public Task<Guid> TransferAsync(
        Guid debitAccountId, Guid creditAccountId, decimal amount, string idempotencyKey, CancellationToken ct) =>
        SubmitAsync("/transfer", new { debitAccountId, creditAccountId, amount, asset }, idempotencyKey, ct);

    public Task CaptureAsync(Guid correlationId, CancellationToken ct) =>
        PostEmptyAsync($"/transfer/{correlationId}/capture", ct);

    public Task VoidAsync(Guid correlationId, CancellationToken ct) =>
        PostEmptyAsync($"/transfer/{correlationId}/void", ct);

    public async Task<WalletBalance?> GetBalanceAsync(Guid accountId, CancellationToken ct)
    {
        using var response = await http.GetAsync(new Uri($"/accounts/{accountId}/balance", UriKind.Relative), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, ct);
        var dto = await response.Content.ReadFromJsonAsync<BalanceResponse>(ct);
        return dto is null ? null : new WalletBalance(dto.AccountId, dto.Asset, dto.Balance, dto.HeldAmount);
    }

    public async Task<WalletHistory> GetHistoryAsync(Guid accountId, int page, int pageSize, CancellationToken ct)
    {
        var uri = new Uri($"/accounts/{accountId}/history?page={page}&pageSize={pageSize}", UriKind.Relative);
        using var response = await http.GetAsync(uri, ct);
        await EnsureSuccessAsync(response, ct);

        var dto = await response.Content.ReadFromJsonAsync<HistoryResponse>(ct);
        if (dto is null)
        {
            return new WalletHistory(page, pageSize, 0, []);
        }

        var items = dto.Items
            .Select(i => new WalletHistoryEntry(i.Id, i.TxId, i.Asset, i.AmountSigned, i.EntryKind, i.Phase, i.CreatedAt))
            .ToList();
        return new WalletHistory(dto.Page, dto.PageSize, dto.TotalCount, items);
    }

    public async Task<string?> GetTransactionStatusAsync(Guid transactionId, CancellationToken ct)
    {
        using var response = await http.GetAsync(new Uri($"/transactions/{transactionId}", UriKind.Relative), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, ct);
        var dto = await response.Content.ReadFromJsonAsync<TransactionResponse>(ct);
        return dto?.Status;
    }

    private async Task<Guid> SubmitAsync(string path, object body, string idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey);

        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        var accepted = await response.Content.ReadFromJsonAsync<AcceptedResponse>(ct);
        return accepted?.TransactionId
            ?? throw new WalletGatewayException((int)response.StatusCode, "Response did not contain a transactionId.");
    }

    private async Task PostEmptyAsync(string path, CancellationToken ct)
    {
        using var response = await http.PostAsync(new Uri(path, UriKind.Relative), content: null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new WalletGatewayException((int)response.StatusCode, body);
    }
}
