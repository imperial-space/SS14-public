using System;
using System.Numerics;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;


using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Wieldable.Components;


namespace Content.Shared.Imperial.Medieval.Ships.Oar;

/// <summary>
/// По идее это должно быть тут, но млять грёбаный делей
/// </summary>
public sealed class OarSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem  _rdWeight = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<OarComponent, AfterInteractEvent>(OnOarAfterInteract);
    }

    private void OnOarAfterInteract(EntityUid uid, OarComponent component, AfterInteractEvent args)
    {
        if (!TryComp<WieldableComponent>(uid, out var wieldableComponent) || wieldableComponent.Wielded == false)
            return;

        var playerEntity = args.User;

        if (args.Handled || !args.CanReach)
            return;

        if (!_skills.HasSkill(playerEntity, SharedSkillsSystem.StrengthId))
            return;

        var boat = _transform.GetParentUid(playerEntity);

        if (boat == args.ClickLocation.EntityId)
            return;

        var clickEntity = args.ClickLocation.EntityId;
        if (boat == _transform.GetParentUid(clickEntity))
            return;
        if (!HasComp<MapComponent>(clickEntity))
            return;

        var time = 7 -_skills.GetSkillLevel(playerEntity, "Agility") * 0.3f;
        time = Math.Max(1.0f, time);
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new OnOarDoAfterEvent(),
            args.Used,
            args.Target,
            args.Used)
        {
            MovementThreshold = 0.1f,
            BreakOnMove = true,
            BlockDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(sdoAfter);
        var playerPosition = _transform.GetWorldPosition(playerEntity);
        var boatPosition = _transform.ToWorldPosition(args.ClickLocation);
        var direction = (playerPosition - boatPosition).ToAngle();
        component.Direction = direction;

        _popup.PopupClient($"Ты гребёшь от себя", playerEntity);

    }
}
