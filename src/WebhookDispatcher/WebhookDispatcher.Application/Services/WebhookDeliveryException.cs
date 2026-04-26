namespace PlatformWallet.WebhookDispatcher.Application.Services;

public sealed class WebhookDeliveryException(int statusCode, string responseBody)
    : Exception($"HTTP {statusCode}: {responseBody[..Math.Min(responseBody.Length, 500)]}")
{
    public int    StatusCode    { get; } = statusCode;
    public string ResponseBody  { get; } = responseBody;
}
