using System;
using System.Numerics;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Robust.Shared.Map;

namespace Content.Server.MagicBarrier;

public sealed class MagicBarrierRiftSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MagicBarrierRiftComponent, AfterInteractUsingEvent>(OnRiftUse);
        SubscribeLocalEvent<RiftGuardianComponent, MobStateChangedEvent>(OnGuardianStateChanged);
    }

    private void OnRiftUse(EntityUid uid, MagicBarrierRiftComponent component, AfterInteractUsingEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        if (!TryComp<RiftKeyComponent>(args.Used, out var keyComponent)
            || !string.Equals(keyComponent.Element, component.Element, StringComparison.OrdinalIgnoreCase))
            return;

        if (component.GuardiansSpawned)
            return;

        component.GuardiansSpawned = true;
        component.Guardians.Clear();

        QueueDel(args.Used);

        for (var i = 0; i < component.GuardianEntities.Count; i++)
        {
            SpawnGuardian(uid, component, i);
        }
        args.Handled = true;
    }

    private void SpawnGuardian(EntityUid rift, MagicBarrierRiftComponent component, int index)
    {
        if (index < 0 || index >= component.GuardianEntities.Count)
            return;

        var offset = index < component.GuardianOffsets.Count
            ? component.GuardianOffsets[index]
            : Vector2.Zero;
        var coords = Transform(rift).Coordinates.Offset(offset);
        var guardian = Spawn(component.GuardianEntities[index], coords);
        var guardianComponent = EnsureComp<RiftGuardianComponent>(guardian);
        guardianComponent.Rift = rift;
        component.Guardians.Add(guardian);
    }

    private void OnGuardianStateChanged(EntityUid uid, RiftGuardianComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<MagicBarrierRiftComponent>(component.Rift, out var riftComponent))
            return;

        riftComponent.Guardians.Remove(uid);
        if (riftComponent.Guardians.Count == 0)
        {
            var coords = Transform(component.Rift).Coordinates;
            Spawn("MedievalSkeletDespawnEffect", coords);
            riftComponent.DestroyedLegitimately = true;
            QueueDel(component.Rift);
        }
    }
}
