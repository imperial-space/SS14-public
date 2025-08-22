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

        SubscribeLocalEvent<CustomChaplainStoreComponent, CustomChaplainShopActionEvent>(OnShop);
    }

    private void OnShop(EntityUid uid, CustomChaplainStoreComponent component, CustomChaplainShopActionEvent args)
    {
        // Открываем магазин способностей кастомного священника
        // Находим StoreComponent на том же entity и используем его для открытия UI
        if (TryComp<StoreComponent>(uid, out var storeComp))
        {
            _store.ToggleUi(uid, uid, storeComp);
        }
    }
}
