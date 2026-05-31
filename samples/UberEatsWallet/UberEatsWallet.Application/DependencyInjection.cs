using Microsoft.Extensions.DependencyInjection;
using UberEatsWallet.Application.Services;
using UberEatsWallet.Application.Webhooks;

namespace UberEatsWallet.Application;

public static class DependencyInjection
{
    /// <summary>Registers the application's use-case services. Ports are bound in Infrastructure.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<WalletService>();
        services.AddScoped<OrderService>();
        services.AddScoped<RefundService>();
        services.AddScoped<WalletWebhookProcessor>();
        return services;
    }
}
