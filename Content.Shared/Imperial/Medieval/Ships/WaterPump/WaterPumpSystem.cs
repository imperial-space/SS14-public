using System;
using System.Threading;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

public sealed class WaterPumpSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedWaterOnShipSystem _waterOnShip = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    /// <inheritdoc/>

    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPumpComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<WaterPumpComponent>();
        while (query.MoveNext(out var pumpUid, out var pump))
            if (pump.UsedTime is { } time && time + TimeSpan.FromSeconds(0.4f) < _timing.CurTime)
            {
                _appearance.SetData(pumpUid, WaterPumpVisuals.State, WaterPumpState.Idle);
                pump.UsedTime = null;
            }
    }

    private void OnActivateInWorld(EntityUid uid, WaterPumpComponent component, ActivateInWorldEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Handled)
            return;

        Use(args.User, uid, component);
        args.Handled = true;
    }

    private void Use(EntityUid playerEntity, EntityUid used, WaterPumpComponent component)
    {
        if (component.User is { } user && user != playerEntity)
            return;
        else if (component.User == playerEntity)
        {
            component.User = null;
            _doAfter.Cancel(component.DoAfter);
            return;
        }

        var query = EntityQueryEnumerator<WaterPumpComponent>();
        while (query.MoveNext(out _, out var pump))
            if (pump.User == playerEntity)
                return;

        var boat = _transform.GetParentUid(used);

        TryComp<MapGridComponent>(boat, out var boatComponent);
        if (boatComponent == null)
            return;

        var time = 7 - _skills.GetSkillLevel(playerEntity, "Agility") * 0.15f - _skills.GetSkillLevel(playerEntity, "Strength") * 0.25f;
        time = Math.Max(1.5f, time);
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new PumpUseEvent(),
            used,
            boat,
            used)
        {
            MovementThreshold = 0.1f,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(sdoAfter, out var doAfterId))
        {
            component.User = playerEntity;
            component.DoAfter = doAfterId;
        }
        else
        {
            component.User = null;
            component.DoAfter = null;
        }
    }
}

[NetSerializable, Serializable]
public enum WaterPumpVisuals : byte
{
    Layer,
    State
}

[NetSerializable, Serializable]
public enum WaterPumpState : byte
{
    Idle,
    Active
}
