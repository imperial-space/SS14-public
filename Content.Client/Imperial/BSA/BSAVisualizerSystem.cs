using Content.Shared.Imperial.BSA;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client.Imperial.BSA;

/// <summary>
/// Пока BSA не собрана — показывает спрайт консоли.
/// После сборки всех частей — переключает на спрайт пушки (cannon_east / cannon_west).
/// </summary>
public sealed class BSAVisualizerSystem : EntitySystem
{
    private EntityQuery<TransformComponent> _xformQuery;
    private enum Layers : byte
    {
        Base   = 0, // Консоль (control_box.rsi)
        Cannon = 1, // Пушка (bluespace_artillery.rsi)
        Top    = 2, // Верхний слой пушки (unshaded)
    }

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<BSAControlBoxComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BSAControlBoxComponent, MoveEvent>(OnMove);
        // Срабатывает при изменении Assembled на клиенте (через AutoNetworkedField)
        SubscribeLocalEvent<BSAControlBoxComponent, AfterAutoHandleStateEvent>(OnMachineState);
    }

    private void OnInit(Entity<BSAControlBoxComponent> ent, ref ComponentInit args)
        => UpdateSprite(ent.Owner, ent.Comp);

    private void OnMove(Entity<BSAControlBoxComponent> ent, ref MoveEvent args)
        => UpdateSprite(ent.Owner, ent.Comp);

    private void OnMachineState(Entity<BSAControlBoxComponent> ent, ref AfterAutoHandleStateEvent args)
        => UpdateSprite(ent.Owner, ent.Comp);

    private void UpdateSprite(EntityUid uid, BSAControlBoxComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;

        var assembled = component.Assembled;

        if (assembled)
        {
            var isEast = Math.Cos(xform.LocalRotation.Theta) >= 0;

            // Скрываем консоль, показываем пушку
            sprite.LayerSetVisible((int) Layers.Base, false);
            sprite.LayerSetVisible((int) Layers.Cannon, true);
            sprite.LayerSetVisible((int) Layers.Top, true);
            sprite.LayerSetState((int) Layers.Cannon, isEast ? "cannon_east" : "cannon_west");
            sprite.LayerSetState((int) Layers.Top,    isEast ? "top_east"    : "top_west");
        }
        else
        {
            // Показываем консоль, скрываем пушку
            sprite.LayerSetVisible((int) Layers.Base,   true);
            sprite.LayerSetVisible((int) Layers.Cannon, false);
            sprite.LayerSetVisible((int) Layers.Top,    false);
        }
    }
}
