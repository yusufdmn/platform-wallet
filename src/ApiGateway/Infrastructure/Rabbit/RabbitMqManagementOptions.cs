namespace PlatformWallet.ApiGateway.Yarp.Infrastructure.Rabbit;

public sealed class RabbitMqManagementOptions
{
    public string BaseUrl  { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Vhost    { get; init; } = "/";
}
