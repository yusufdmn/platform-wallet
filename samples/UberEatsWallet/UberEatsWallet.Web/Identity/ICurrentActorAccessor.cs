namespace UberEatsWallet.Web.Identity;

/// <summary>Reads/writes the "acting as" identity from the session cookie.</summary>
public interface ICurrentActorAccessor
{
    CurrentActor? Current { get; }

    void SignIn(CurrentActor actor);

    void SignOut();
}
