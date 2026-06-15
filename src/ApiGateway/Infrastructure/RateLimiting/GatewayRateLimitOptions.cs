namespace PlatformWallet.ApiGateway.Yarp.Infrastructure.RateLimiting;

/// <summary>
/// Token-bucket rate-limit settings for the gateway, bound from environment configuration.
/// The bucket starts full at <see cref="TokenLimit"/> (the maximum burst); every
/// <see cref="ReplenishmentPeriod"/> it is refilled by <see cref="TokensPerPeriod"/> tokens,
/// which governs the sustained request rate.
/// </summary>
public sealed class GatewayRateLimitOptions
{
    public const string TokenLimitKey       = "RATE_LIMIT_TOKEN_LIMIT";
    public const string TokensPerPeriodKey  = "RATE_LIMIT_TOKENS_PER_PERIOD";
    public const string ReplenishSecondsKey = "RATE_LIMIT_REPLENISH_SECONDS";

    private const int DefaultTokenLimit       = 100;
    private const int DefaultTokensPerPeriod  = 10;
    private const int DefaultReplenishSeconds = 6;

    /// <summary>Maximum number of tokens the bucket can hold; the largest burst allowed.</summary>
    public int TokenLimit { get; private init; }

    /// <summary>Tokens added back to the bucket each <see cref="ReplenishmentPeriod"/>.</summary>
    public int TokensPerPeriod { get; private init; }

    /// <summary>How often <see cref="TokensPerPeriod"/> tokens are replenished.</summary>
    public TimeSpan ReplenishmentPeriod { get; private init; }

    public static GatewayRateLimitOptions FromConfiguration(IConfiguration configuration) => new()
    {
        TokenLimit          = ReadPositiveInt(configuration, TokenLimitKey, DefaultTokenLimit),
        TokensPerPeriod     = ReadPositiveInt(configuration, TokensPerPeriodKey, DefaultTokensPerPeriod),
        ReplenishmentPeriod = TimeSpan.FromSeconds(
            ReadPositiveInt(configuration, ReplenishSecondsKey, DefaultReplenishSeconds)),
    };

    private static int ReadPositiveInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
}
