using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Cult.Components;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Cult;

/// <summary>Жнец (amputating_limbs, seek_prey), seek_master, самонаведение малой магической ракеты.</summary>
public sealed partial class CultConstructSystem
{

    private static readonly EntProtoId SeekMasterAction = "ActionCultSeekMaster";
    private static readonly EntProtoId SeekPreyAction = "ActionCultSeekPrey";

    /// <summary>BODY_ZONE_L_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_ARM, BODY_ZONE_R_LEG вместе с кистями/стопами.</summary>
    private static readonly (ProtoId<OrganCategoryPrototype> Limb, ProtoId<OrganCategoryPrototype> Extremity)[] Limbs =
    {
        ("ArmLeft", "HandLeft"),
        ("LegLeft", "FootLeft"),
        ("ArmRight", "HandRight"),
        ("LegRight", "FootRight"),
    };

    private void InitializeHarvester()
    {
        SubscribeLocalEvent<CultConstructComponent, CultSeekMasterActionEvent>(OnSeekMaster);
        SubscribeLocalEvent<CultConstructComponent, CultSeekPreyActionEvent>(OnSeekPrey);
        SubscribeLocalEvent<CultMissileProjectileComponent, PreventCollideEvent>(OnMissileCollide);
    }

    /// <summary>harvester/resolve_unarmed_attack + amputating_limbs: отрывает конечность или сбивает с ног.</summary>
    private bool TryHarvest(EntityUid harvester, EntityUid target)
    {
        if (!HasComp<HumanoidProfileComponent>(target))
            return false;
        if (!_container.TryGetContainer(target, BodyComponent.ContainerID, out var organs))
            return false;

        var limbs = organs.ContainedEntities
            .Where(o => TryComp<OrganComponent>(o, out var organ) && organ.Category is { } cat && Limbs.Any(l => l.Limb == cat))
            .ToList();

        if (limbs.Count > 0)
        {
            var limb = _random.Pick(limbs);
            var category = Comp<OrganComponent>(limb).Category!.Value;
            var extremity = Limbs.First(l => l.Limb == category).Extremity;
            foreach (var organ in organs.ContainedEntities.ToList())
            {
                if (organ == limb || TryComp<OrganComponent>(organ, out var oc) && oc.Category == extremity)
                    _container.Remove(organ, organs, force: true);
            }
            return true;
        }

        _cult.Paralyze(target, 6);
        _popup.PopupEntity(Loc.GetString("cult-harvester-knockdown", ("user", harvester), ("target", target)), target, PopupType.MediumCaution);
        _cult.Message(harvester, Loc.GetString("cult-harvester-bring", ("target", target)));
        return true;
    }

    /// <summary>make_new_construct(): seek_master для созданных существом конструктов.</summary>
    private void GrantSeekMaster(EntityUid construct, CultConstructComponent comp)
    {
        EntityUid? action = null;
        if (_cultActions().AddAction(construct, ref action, SeekMasterAction) && action != null)
            comp.SeekAction = action;
    }

    private void OnSeekMaster(Entity<CultConstructComponent> ent, ref CultSeekMasterActionEvent args)
    {
        args.Handled = true;
        var rule = _cult.GetRule();
        if (rule is { } r && r.Comp.NarSieSummoned && r.Comp.BloodTarget is { } blood)
            ent.Comp.Master = blood;

        if (ent.Comp.Master == null || TerminatingOrDeleted(ent.Comp.Master.Value))
        {
            _cult.Message(ent, Loc.GetString("cult-seek-master-none"));
            ent.Comp.Seeking = false;
            return;
        }

        if (ent.Comp.Seeking)
        {
            ent.Comp.Seeking = false;
            _cult.Message(ent, Loc.GetString("cult-seek-master-off"));
            return;
        }

        ent.Comp.Seeking = true;
        ent.Comp.SeekTarget = ent.Comp.Master;
        _cult.Message(ent, Loc.GetString("cult-seek-master-on"));
    }

    /// <summary>seek_prey: жнец выслеживает душу, которую жаждет Нар'Си.</summary>
    private void OnSeekPrey(Entity<CultConstructComponent> ent, ref CultSeekPreyActionEvent args)
    {
        args.Handled = true;
        var narsie = EntityQuery<CultNarSieComponent>().FirstOrDefault();
        if (narsie == null)
            return;

        if (ent.Comp.Seeking)
        {
            ent.Comp.Seeking = false;
            _cultActions().SetIcon(args.Action.Owner, new Robust.Shared.Utility.SpriteSpecifier.Rsi(new("/Textures/Imperial/BloodCult/actions.rsi"), "cult_mark"));
            _cult.Message(ent, Loc.GetString("cult-seek-prey-narsie"));
            return;
        }

        narsie.SoulsNeeded.RemoveWhere(s => TerminatingOrDeleted(s));
        if (narsie.SoulsNeeded.Count == 0)
        {
            _cult.Message(ent, Loc.GetString("cult-seek-prey-done"));
            return;
        }

        var prey = _random.Pick(narsie.SoulsNeeded.ToList());
        ent.Comp.Master = prey;
        ent.Comp.SeekTarget = prey;
        ent.Comp.Seeking = true;
        _cultActions().SetIcon(args.Action.Owner, new Robust.Shared.Utility.SpriteSpecifier.Rsi(new("/Textures/Imperial/BloodCult/actions.rsi"), "sintouch"));
        _cult.Message(ent, Loc.GetString("cult-seek-prey-on", ("target", prey)));
    }

    /// <summary>harvester/grant_abilities(): seek.Activate() при создании.</summary>
    private void StartHarvesterSeek(EntityUid harvester)
    {
        if (!TryComp<CultConstructComponent>(harvester, out var comp))
            return;
        foreach (var action in comp.GrantedActions)
        {
            if (MetaData(action).EntityPrototype is not { } actionProto || actionProto.ID != SeekPreyAction.Id)
                continue;
            var ev = new CultSeekPreyActionEvent();
            ev.Action = (action, Comp<Content.Shared.Actions.Components.ActionComponent>(action));
            RaiseLocalEvent(harvester, ev);
        }
    }

    /// <summary>can_only_hit_target: ракета пролетает сквозь чужие цели.</summary>
    private void OnMissileCollide(Entity<CultMissileProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Target is not { } target || args.OtherEntity == target)
            return;
        if (HasComp<Content.Shared.Mobs.Components.MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    /// <summary>Самонаведение магической ракеты (projectile homing).</summary>
    private void UpdateMissiles()
    {
        var query = EntityQueryEnumerator<CultMissileProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var missile, out var physics, out var xform))
        {
            if (missile.Target is not { } target || TerminatingOrDeleted(target))
                continue;
            var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform);
            if (delta.LengthSquared() < 0.01f)
                continue;
            var velocity = Vector2.Normalize(delta) * missile.Speed;
            _physics.SetLinearVelocity(uid, velocity, body: physics);
            _transform.SetWorldRotation(uid, velocity.ToWorldAngle());
        }
    }
}
