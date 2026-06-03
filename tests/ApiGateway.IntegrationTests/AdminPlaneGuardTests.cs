using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace PlatformWallet.ApiGateway.IntegrationTests;

[Trait("Category", "Integration")]
public class AdminPlaneGuardTests
{
    // The TestServer connection reports LocalPort 0, which never equals the configured
    // internal listener port — so every admin-plane request here arrives as if on the
    // public listener and must be refused with a flat 404 (no auth challenge, no hint).
    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.UseSetting("REDIS_CONNECTION",   "localhost:6379");
                host.UseSetting("KEYCLOAK_AUTHORITY", "http://localhost/fake-keycloak");

                host.ConfigureServices(services =>
                {
                    services.RemoveAll<IDistributedCache>();
                    services.AddDistributedMemoryCache();
                });
            });
    }

    [Theory]
    [InlineData("/console/")]
    [InlineData("/console/config.json")]
    [InlineData("/admin/dlq/")]
    [InlineData("/admin/sagas")]
    public async Task Admin_plane_paths_return_404_when_not_on_internal_listener(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the admin plane is confined to the internal listener; the public listener must 404 it");
    }

    [Fact]
    public async Task Public_data_plane_is_unaffected_by_the_guard()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // /healthz is neither /console nor /admin — the guard must let it through.
        var response = await client.GetAsync("/healthz");

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "non-admin paths must pass the guard regardless of listener port");
    }
}
