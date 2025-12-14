using Content.Server.Imperial.Medieval.Spawners.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Medieval.Spawners.EntitySystems;

public sealed class SpawnMultipleOnDespawnSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnMultipleOnDespawnComponent, TimedDespawnEvent>(OnDespawn);
    }

    private void OnDespawn(EntityUid uid, SpawnMultipleOnDespawnComponent comp, ref TimedDespawnEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        foreach (var item in comp.Prototypes)
            Spawn(item, xform.Coordinates);
    }
}
