using Robust.Shared.Timing;
using Content.Shared.IgnitionSource;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Inventory;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos.Components;

namespace Content.Server.Imperial.Medieval;

public sealed class IgnitPocketFlamesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlammableSystem _flammableSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    public override void Initialize()
    {
        base.Initialize();

    }
    TimeSpan StartTime = TimeSpan.FromSeconds(0f);
    TimeSpan EndTime = TimeSpan.FromSeconds(0f);
    TimeSpan ReloadTime = TimeSpan.FromSeconds(15f);
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime > EndTime)
        {
            StartTime = _timing.CurTime;
            EndTime = StartTime + ReloadTime;

            foreach (var comp in EntityManager.EntityQuery<IgnitPocketFlamesComponent>())
            {
                var xform = Transform(comp.Owner);
                var coords = xform.Coordinates;

                var pockets = _inventory.GetInventoryEntities(comp.Owner, SlotFlags.All);
                foreach (var pocket in pockets)
                {
                    if (!TryComp<IgnitionSourceComponent>(pocket, out var source) || !source.Ignited)
                        continue;

                    if (!TryComp<FlammableComponent>(comp.Owner, out var flammable))
                        continue;

                    _flammableSystem.AdjustFireStacks(comp.Owner, comp.Power, flammable);
                    _flammableSystem.Ignite(comp.Owner, source.Owner);
                }

            }
        }
    }

}
