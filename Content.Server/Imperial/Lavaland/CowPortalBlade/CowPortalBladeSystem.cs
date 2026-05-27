using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.CowPortalBlade;

/// <summary>
/// Клинок Портала: при использовании в руке (Z) спавнит <see cref="CowPortalComponent"/>
/// у ног игрока. Кулдаун — <see cref="CowPortalBladeComponent.CooldownSeconds"/> секунд.
/// </summary>
public sealed class CowPortalBladeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CowPortalBladeComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<CowPortalBladeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (_timing.CurTime < ent.Comp.NextUseTime)
            return;

        ent.Comp.NextUseTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.CooldownSeconds);
        args.Handled = true;

        var coords = Transform(args.User).Coordinates;
        SpawnAtPosition(ent.Comp.PortalPrototype, coords);
    }
}
