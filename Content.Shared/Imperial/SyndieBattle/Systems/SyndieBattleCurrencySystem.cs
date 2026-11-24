using Content.Shared.Imperial.SyndieBattle.Components;
using Content.Shared.Store;

namespace Content.Shared.Imperial.SyndieBattle.Systems;

/// <summary>
/// Prevents physical currency from being inserted into Syndie Battle stores on both client and server.
/// </summary>
public sealed class SyndieBattleCurrencySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SyndieBattlePdaComponent, CurrencyInsertAttemptEvent>(OnCurrencyInsertAttempt);
    }

    private void OnCurrencyInsertAttempt(EntityUid uid, SyndieBattlePdaComponent component, CurrencyInsertAttemptEvent args)
    {
        args.Cancel();
    }
}
