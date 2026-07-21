using System;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Sail;

public sealed class SailSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;

    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<SailComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SailComponent, SailFoldDoAfterEvent>(OnFold);
        SubscribeLocalEvent<SailComponent, SailRotateDoAfterEvent>(OnRotate);
        SubscribeLocalEvent<SailComponent, SailMenuActionMessage>(OnMenuAction);
        SubscribeLocalEvent<SailComponent, ExaminedEvent>(OnExamine);
    }

    private void OnStartup(EntityUid uid, SailComponent component, ComponentStartup args)
    {
        UpdateSailVisuals(uid, component);

        var sailXform = Transform(uid);
        if (!TryGetGrid(uid, sailXform, out var boat))
            return;

        if (HasComp<ImplicitRoofComponent>(boat))
            RemComp<ImplicitRoofComponent>(boat);
    }

    private void OnMenuAction(EntityUid uid, SailComponent component, SailMenuActionMessage args)
    {
        var player = args.Actor;
        if (!_actionBlocker.CanInteract(player, uid) ||
            !_interaction.InRangeAndAccessible(player, uid))
            return;

        switch (args.Action)
        {
            case SailMenuAction.RotateLeft:
                TryRotate(player, uid, true);
                break;
            case SailMenuAction.ToggleFold:
                TryFold(player, uid);
                break;
            case SailMenuAction.RotateRight:
                TryRotate(player, uid, false);
                break;
        }
    }

    private void TryRotate(EntityUid player, EntityUid sail, bool rotateLeft)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, player, GetInteractionTime(player), new SailRotateDoAfterEvent(rotateLeft), sail, sail)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void TryFold(EntityUid player, EntityUid sail)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, player, GetInteractionTime(player), new SailFoldDoAfterEvent(), sail, sail)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private float GetInteractionTime(EntityUid player)
    {
        var time = 7f - _skills.GetSkillLevel(player, "Agility") * 0.15f -
            _skills.GetSkillLevel(player, "Intelligence") * 0.15f;
        return Math.Max(1f, time);
    }

    private void OnExamine(EntityUid uid, SailComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("sail-examine-efficiency", ("efficiency", FormatEfficiency(component.LastSailEfficencyMod))));
        args.PushMarkup(Loc.GetString("sail-examine-wind-strength", ("strength", FormatEfficiency(_cfg.GetCVar(ShipsCCVars.WindPower)))));
    }

    private void OnRotate(EntityUid uid, SailComponent component, SailRotateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TransformComponent>(uid, out var transformComponent))
            return;

        var delta = args.RotateLeft ? 45f : -45f;
        var newAngle = transformComponent.LocalRotation + Angle.FromDegrees(delta);
        _transform.SetLocalRotation(uid, newAngle);
        _audio.PlayPvs(_random.Pick(MedievalShipSounds.SailRotate), uid);
        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime <= _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WindDelay));
        if (!_cfg.GetCVar(ShipsCCVars.WindEnabled))
        {
            ResetSailEfficiency();
            return;
        }

        var windDirection = Angle.FromDegrees(_cfg.GetCVar(ShipsCCVars.WindRotation));
        var stormLevel = _cfg.GetCVar(ShipsCCVars.StormLevel);
        var windPower = _cfg.GetCVar(ShipsCCVars.WindPower);

        foreach (var sailComponent in EntityManager.EntityQuery<SailComponent>())
        {
            var sailEntity = sailComponent.Owner;
            var sailXform = Transform(sailEntity);

            if (!TryGetGrid(sailEntity, sailXform, out var boat))
            {
                SetLastSailEfficencyMod(sailEntity, sailComponent, 0f);
                continue;
            }

            if (sailComponent.Folded)
            {
                SetLastSailEfficencyMod(sailEntity, sailComponent, 0f);
                continue;
            }

            if (HasComp<IslandComponent>(boat))
            {
                SetLastSailEfficencyMod(sailEntity, sailComponent, 0f);
                continue;
            }

            var mapUid = _transform.GetMap(boat);
            if (!mapUid.HasValue || !TryComp<SeaComponent>(mapUid.Value, out var sea))
            {
                SetLastSailEfficencyMod(sailEntity, sailComponent, 0f);
                continue;
            }

            EnsureComp<ShipDrowningComponent>(boat);

            if (!sea.WindEnabledLocal)
            {
                SetLastSailEfficencyMod(sailEntity, sailComponent, 0f);
                continue;
            }

            if (!sailComponent.Push)
            {
                _transform.SetWorldRotation(sailEntity, windDirection);
                SetLastSailEfficencyMod(sailEntity, sailComponent, GetForceFactorByAngle(_transform.GetWorldRotation(sailEntity), windDirection));
                continue;
            }

            var sailDirection = _transform.GetWorldRotation(sailEntity);
            var forceFactor = GetForceFactorByAngle(sailDirection, windDirection);
            SetLastSailEfficencyMod(sailEntity, sailComponent, forceFactor);

            if (TryComp<ShuttleComponent>(boat, out var shuttle) && !shuttle.Enabled)
                continue;

            if (GetShipSpeed(boat) >= _cfg.GetCVar(ShipsCCVars.ShipsMaxSpeed))
                continue;

            var shipDirection = _transform.GetWorldRotation(boat);
            if (MathF.Abs(forceFactor) < 0.001f)
                continue;

            if (!TryComp<MapGridComponent>(boat, out var mapGrid))
                continue;

            var overloadCeil = ShipWeightHelper.GetMaxWeight(boat, mapGrid, _map, EntityManager, _cfg);
            if (overloadCeil < 0)
                continue;

            var weight = _rdWeight.GetTotalOnGrid(boat);
            var impulseMagnitude = GetImpulseMagnitude(stormLevel * windPower * sailComponent.SailSize, overloadCeil, weight);
            var localImpulse = Vector2.UnitY * (impulseMagnitude * forceFactor);
            var worldImpulse = shipDirection.RotateVec(localImpulse);

            if (!TryComp<PhysicsComponent>(boat, out var body))
                continue;

            _physics.WakeBody(boat);
            _physics.ApplyLinearImpulse(boat, worldImpulse, body: body);
        }
    }

    private void ResetSailEfficiency()
    {
        foreach (var sailComponent in EntityManager.EntityQuery<SailComponent>())
        {
            SetLastSailEfficencyMod(sailComponent.Owner, sailComponent, 0f);
        }
    }

    private void SetLastSailEfficencyMod(EntityUid uid, SailComponent component, float mod)
    {
        if (MathF.Abs(component.LastSailEfficencyMod - mod) < 0.001f)
            return;

        component.LastSailEfficencyMod = mod;
        Dirty(uid, component);
    }

    private bool TryGetGrid(EntityUid uid, TransformComponent xform, out EntityUid grid)
    {
        grid = _transform.GetMoverCoordinates(uid, xform).EntityId;
        return HasComp<MapGridComponent>(grid);
    }

    private static string FormatEfficiency(float value)
    {
        return value.ToString("0.##");
    }

    private static float GetForceFactorByAngle(Angle sailDirection, Angle windDirection)
    {
        var diff = MathF.Abs((float) Angle.ShortestDistance(sailDirection, windDirection).Degrees);

        if (diff < 30f)
            return 1f;
        if (diff < 75f)
            return 0.5f;
        if (diff < 115f)
            return 0f;
        if (diff <= 150f)
            return -0.5f;

        return -1f;
    }

    private float GetShipSpeed(EntityUid boat)
    {
        return _physics.GetMapLinearVelocity(boat).Length();
    }

    private static float GetImpulseMagnitude(float power, float overloadCeil, float weight)
    {
        if (weight <= 0f || weight <= overloadCeil)
            return power;

        return power * overloadCeil / weight;
    }

    private void OnFold(EntityUid uid, SailComponent component, SailFoldDoAfterEvent args)
    {
        if (args.Cancelled || TerminatingOrDeleted(uid))
            return;

        component.Folded = !component.Folded;
        Dirty(uid, component);
        UpdateSailVisuals(uid, component);
        _audio.PlayPvs(component.Folded ? MedievalShipSounds.SailClose : MedievalShipSounds.SailOpen, uid);
        args.Handled = true;
    }

    private void UpdateSailVisuals(EntityUid uid, SailComponent component)
    {
        _appearance.SetData(uid, SailVisuals.Folded, component.Folded);
    }
}
