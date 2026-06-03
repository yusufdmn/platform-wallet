using System.Data;
using Dapper;

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Dapper;

/// <summary>
/// Maps PostgreSQL <c>timestamp with time zone</c> — which Npgsql materialises as a UTC
/// <see cref="DateTime"/> — onto <see cref="DateTimeOffset"/>. Without this, Dapper throws
/// <see cref="InvalidCastException"/> when constructing a record that has a <see cref="DateTimeOffset"/>
/// member (e.g. <c>PostingHistoryItem.CreatedAt</c>), which is why the postings/history read failed
/// while the balance read (no timestamp column) succeeded.
/// </summary>
internal sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset offset => offset,
        DateTime utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)),
        _ => throw new InvalidCastException(
            $"Cannot convert '{value?.GetType().FullName ?? "null"}' to DateTimeOffset."),
    };

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
        parameter.Value = value;
}
