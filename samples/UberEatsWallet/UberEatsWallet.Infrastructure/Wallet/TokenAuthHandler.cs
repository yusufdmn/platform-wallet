using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace UberEatsWallet.Infrastructure.Wallet;

/// <summary>
/// Attaches a cached <c>client_credentials</c> bearer token to every wallet request, refreshing it
/// shortly before expiry. A single in-flight fetch is guarded so concurrent requests don't stampede
/// the token endpoint.
/// </summary>
internal sealed class TokenAuthHandler(
    IHttpClientFactory httpClientFactory,
    IOptions<WalletOptions> options) : DelegatingHandler
{
    public const string TokenClientName = "wallet-token";
    private const int ExpiryBufferSeconds = 30;

    private readonly SemaphoreSlim gate = new(1, 1);
    private string? cachedToken;
    private DateTimeOffset expiresAt;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (HasValidToken())
        {
            return cachedToken!;
        }

        await gate.WaitAsync(ct);
        try
        {
            if (!HasValidToken())
            {
                await FetchTokenAsync(ct);
            }

            return cachedToken!;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool HasValidToken() => cachedToken is not null && DateTimeOffset.UtcNow < expiresAt;

    private async Task FetchTokenAsync(CancellationToken ct)
    {
        var opts = options.Value;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = opts.ClientId,
            ["client_secret"] = opts.ClientSecret,
            ["scope"] = opts.Scopes,
        });

        var client = httpClientFactory.CreateClient(TokenClientName);
        using var response = await client.PostAsync(new Uri(opts.TokenUrl), form, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Token endpoint returned an empty response.");

        cachedToken = payload.AccessToken;
        expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn - ExpiryBufferSeconds);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
