using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Пересчитывает скорость владельца, когда он встаёт на святой скейтборд или сходит с него.
/// </summary>
public sealed class NullRodSkateboardSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodSkateboardComponent, ItemToggledEvent>(OnToggled, after: new[] { typeof(ComponentTogglerSystem) });
    }

    private void OnToggled(Entity<NullRodSkateboardComponent> board, ref ItemToggledEvent args)
    {
        var holder = Transform(board).ParentUid;
        if (holder.IsValid())
            _movementSpeed.RefreshMovementSpeedModifiers(holder);
    }
}
