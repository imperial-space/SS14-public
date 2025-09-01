using Content.Server.Store.Systems;
using Content.Shared.Actions;
using Content.Shared.Store.Components;
using Content.Shared.Imperial.CustomChaplain.Components;
using Robust.Shared.Player;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

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

        // Подписываемся на событие на игроке (performer), а не на компоненте
        SubscribeLocalEvent<CustomChaplainShopActionEvent>(OnShop);

        Log.Info("CustomChaplainShopSystem initialized!");
    }

    private void OnShop(CustomChaplainShopActionEvent args)
    {
        var performer = args.Performer;

        Log.Info($"CustomChaplainShopActionEvent received from performer: {performer}");

        // Открываем магазин способностей кастомного священника
        // Сначала проверяем, есть ли StoreComponent на игроке
        if (!TryComp<StoreComponent>(performer, out var storeComp))
        {
            Log.Info($"No StoreComponent found on performer {performer}, creating one");

            // Создаем StoreComponent на игроке
            storeComp = EntityManager.AddComponent<StoreComponent>(performer);

            // Добавляем категорию CustomChaplainAbilities
            storeComp.Categories.Add(new ProtoId<Content.Shared.Store.StoreCategoryPrototype>("CustomChaplainAbilities"));

            Log.Info($"Created StoreComponent on performer {performer}");
        }

        // Теперь открываем UI магазина
        Log.Info($"Opening shop UI for performer {performer}");
        _store.ToggleUi(performer, performer, storeComp);
    }
}
