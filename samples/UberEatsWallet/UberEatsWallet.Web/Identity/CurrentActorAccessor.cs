namespace UberEatsWallet.Web.Identity;

internal sealed class CurrentActorAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentActorAccessor
{
    private const string TypeKey = "actor.type";
    private const string IdKey = "actor.id";
    private const string NameKey = "actor.name";
    private const string WalletKey = "actor.wallet";

    public CurrentActor? Current
    {
        get
        {
            var session = httpContextAccessor.HttpContext?.Session;
            if (session is null)
            {
                return null;
            }

            if (!Enum.TryParse<ActorType>(session.GetString(TypeKey), out var type) ||
                !Guid.TryParse(session.GetString(IdKey), out var id) ||
                !Guid.TryParse(session.GetString(WalletKey), out var walletAccountId))
            {
                return null;
            }

            return new CurrentActor(type, id, session.GetString(NameKey) ?? string.Empty, walletAccountId);
        }
    }

    public void SignIn(CurrentActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var session = Session();
        session.SetString(TypeKey, actor.Type.ToString());
        session.SetString(IdKey, actor.Id.ToString());
        session.SetString(NameKey, actor.Name);
        session.SetString(WalletKey, actor.WalletAccountId.ToString());
    }

    public void SignOut() => httpContextAccessor.HttpContext?.Session.Clear();

    private ISession Session() =>
        httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("No active HTTP context.");
}
