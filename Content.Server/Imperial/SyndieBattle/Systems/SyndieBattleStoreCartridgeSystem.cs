using Content.Server.Imperial.SyndieBattle.Components;
using Content.Server.Store.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.Imperial.SyndieBattle.Components;
using Content.Shared.Imperial.SyndieBattle.Events;
using Content.Shared.PDA;
using Content.Shared.Store.Components;
using Content.Shared.Store;
using Content.Shared.UserInterface;

namespace Content.Server.Imperial.SyndieBattle.Systems;

public sealed class SyndieBattleStoreCartridgeSystem : EntitySystem
{
    [Dependency] private readonly StoreSystem _storeSystem = default!;
    [Dependency] private readonly SyndieBattleRuleSystem _ruleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SyndieBattleStoreCartridgeComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<SyndieBattlePdaOwnerComponent, ActivatableUIOpenAttemptEvent>(OnStoreOpenAttempt);
        SubscribeLocalEvent<SyndieBattlePdaOwnerComponent, BoundUserInterfaceMessageAttempt>(OnStoreMessageAttempt);
    }

    private void OnStoreOpenAttempt(EntityUid uid, SyndieBattlePdaOwnerComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!_ruleSystem.CanUseStore((uid, component), args.User))
            args.Cancel();
    }

    private void OnStoreMessageAttempt(EntityUid uid, SyndieBattlePdaOwnerComponent component, BoundUserInterfaceMessageAttempt args)
    {
        if (args.UiKey.Equals(StoreUiKey.Key) && !_ruleSystem.CanUseStore((uid, component), args.Actor))
            args.Cancel();
    }

    private void OnMessage(EntityUid uid, SyndieBattleStoreCartridgeComponent component, CartridgeMessageEvent args)
    {
        var pda = GetEntity(args.LoaderUid);
        if (args is not SyndieBattleOpenStoreEvent ||
            Deleted(pda) ||
            !HasComp<PdaComponent>(pda) ||
            !HasComp<SyndieBattlePdaComponent>(pda) ||
            !TryComp<SyndieBattlePdaOwnerComponent>(pda, out var owner) ||
            !_ruleSystem.CanUseStore((pda, owner), args.Actor) ||
            !TryComp<CartridgeLoaderComponent>(pda, out var loader) ||
            loader.ActiveProgram != uid ||
            !TryComp<StoreComponent>(pda, out var store))
        {
            return;
        }

        _storeSystem.ToggleUi(args.Actor, pda, store);
    }
}
