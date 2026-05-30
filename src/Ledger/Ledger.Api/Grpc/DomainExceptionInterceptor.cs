using Grpc.Core;
using Grpc.Core.Interceptors;
using PlatformWallet.Ledger.Domain.Exceptions;

namespace PlatformWallet.Ledger.Api.Grpc;

public sealed class DomainExceptionInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (LedgerDomainException ex)
        {
            throw new RpcException(new Status(MapStatusCode(ex), ex.Message));
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (LedgerDomainException ex)
        {
            throw new RpcException(new Status(MapStatusCode(ex), ex.Message));
        }
    }

    private static StatusCode MapStatusCode(LedgerDomainException ex) => ex switch
    {
        AccountNotFoundException or SystemAccountNotFoundException => StatusCode.NotFound,
        InvalidAccountIdException or InvalidAmountException        => StatusCode.InvalidArgument,
        _                                                          => StatusCode.FailedPrecondition,
    };
}
