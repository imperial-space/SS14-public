using Content.Server.Store.Systems;
using Content.Shared.Actions;
using Content.Shared.Store.Components;
using Content.Shared.Imperial.CustomChaplain.Components;
using Robust.Shared.Player;

namespace Content.Server.Imperial.CustomChaplain.Systems;

/// <summary>
/// System for handling Custom Chaplain ability shop functionality.
/// </summary>
public sealed class CustomChaplainShopSystem : EntitySystem
{
    [Dependency] private readonly StoreSystem _store = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomChaplainShopActionEvent>(OnShop);
    }

    private void OnShop(CustomChaplainShopActionEvent args)
    {
        var performer = args.Performer;

        // Ищем StoreComponent на performer
        if (!TryComp<StoreComponent>(performer, out var store))
            return;

        _store.ToggleUi(performer, performer, store);
    }
}
