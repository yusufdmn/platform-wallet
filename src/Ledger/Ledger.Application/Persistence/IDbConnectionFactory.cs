using System.Data;

namespace PlatformWallet.Ledger.Application.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
