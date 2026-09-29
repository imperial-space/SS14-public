using System.Linq;
using System.Numerics;
using System.Threading;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Beam;
using Content.Server.Body;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Damage.Systems;
using Content.Server.Decals;
using Content.Server.DoAfter;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Electrocution;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Medical;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.SSDIndicator;
using Content.Shared.Slippery;
using Content.Shared.Speech.Muting;
using Content.Shared.Stacks;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Reflect;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.Imperial.Heretic.Actions;

/// <summary>
/// Общие способности еретика, не привязанные к пути: Покров тьмы, Сердцебиение Мансуса, призыв фамильяра, Необузданное искусство.
/// </summary>
public sealed class HereticGeneralActionsSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly IRobustRandom         _random  = default!;
    [Dependency] private readonly SharedStealthSystem   _stealth = default!;
    [Dependency] private readonly SharedTransformSystem _xform   = default!;
    [Dependency] private readonly HereticSystem         _heretic = default!;
    [Dependency] private readonly MindSystem            _mind    = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly SharedContainerSystem  _container = default!;
    [Dependency] private readonly SharedActionsSystem    _actions      = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly MetaDataSystem              _metaData      = default!;

    private static readonly EntProtoId DisableCloakProto = "ActionHereticDisableCloak";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticCloakOfShadowActionEvent>(OnCloakOfShadow);
        SubscribeLocalEvent<HereticComponent, HereticDisableCloakActionEvent>(OnDisableCloak);
        SubscribeLocalEvent<HereticComponent, HereticHeartbeatMansusActionEvent>(OnHeartbeatMansus);
        SubscribeLocalEvent<HereticComponent, HereticRelentlessHeartbeatActionEvent>(OnRelentlessHeartbeat);
        SubscribeLocalEvent<HereticComponent, HereticSummonFamiliarActionEvent>(OnSummonFamiliar);
        SubscribeLocalEvent<HereticComponent, HereticUnsealedArtsActionEvent>(OnUnsealedArts);
    }

    // ─── General ──────────────────────────────────────────────────────────────

    private void OnCloakOfShadow(EntityUid uid, HereticComponent comp, HereticCloakOfShadowActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        const int durationSeconds = 180;
        args.Handled = true;

        var origWalk   = 2.5f;
        var origSprint = 4.5f;
        var origAccel  = 20f;
        if (TryComp<MovementSpeedModifierComponent>(uid, out var moveComp))
        {
            origWalk   = moveComp.BaseWalkSpeed;
            origSprint = moveComp.BaseSprintSpeed;
            origAccel  = moveComp.Acceleration;
        }
        var originalName = MetaData(uid).EntityName;

        // Effects: smoke + sound + popup
        Spawn("HereticEffectSmoke", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-cloak-of-shadow"), uid, uid, PopupType.Medium);

        // Spawn persistent curse overlay parented to the player
        var effectUid = Spawn("HereticCloakEffect", Transform(uid).Coordinates);
        _xform.SetParent(effectUid, uid);

        // Rename player, apply 25% speed boost, hide original sprite
        _metaData.SetEntityName(uid, "Тень");
        _movementSpeed.ChangeBaseSpeed(uid, origWalk, origSprint * 1.25f, origAccel);
        var addedStealth = !HasComp<StealthComponent>(uid);
        var stealthComp = EnsureComp<StealthComponent>(uid);
        _stealth.SetEnabled(uid, true, stealthComp);
        _stealth.SetVisibility(uid, -1f, stealthComp);

        // Store deactivation data in component
        var cloakComp = EnsureComp<HereticCloakActiveComponent>(uid);
        cloakComp.OrigWalkSpeed    = origWalk;
        cloakComp.OrigSprintSpeed  = origSprint;
        cloakComp.OrigAcceleration = origAccel;
        cloakComp.OriginalName     = originalName;
        cloakComp.EffectEntity     = effectUid;
        cloakComp.AddedStealth     = addedStealth;
        cloakComp.IsActive         = true;

        // Grant the manual-disable action
        EntityUid? disableAction = null;
        _actions.AddAction(uid, ref disableAction, DisableCloakProto);
        cloakComp.DisableActionEntity = disableAction;

        // Curse icon trail
        void SpawnTrail()
        {
            if (!cloakComp.IsActive || Deleted(uid)) return;
            Spawn("HereticCloakTrailEffect", Transform(uid).Coordinates);
            Timer.Spawn(TimeSpan.FromMilliseconds(100), SpawnTrail);
        }
        Timer.Spawn(TimeSpan.FromMilliseconds(100), SpawnTrail);

        Timer.Spawn(TimeSpan.FromSeconds(durationSeconds), () =>
        {
            if (Deleted(uid)) return;
            if (!TryComp<HereticCloakActiveComponent>(uid, out var cc)) return;
            if (!cc.IsActive) return;
            DeactivateCloak(uid, cc);
        });
    }

    private void OnDisableCloak(EntityUid uid, HereticComponent comp, HereticDisableCloakActionEvent args)
    {
        if (args.Handled) return;
        if (!TryComp<HereticCloakActiveComponent>(uid, out var cloakComp)) return;
        args.Handled = true;
        DeactivateCloak(uid, cloakComp);
    }

    private void DeactivateCloak(EntityUid uid, HereticCloakActiveComponent cloakComp)
    {
        if (!cloakComp.IsActive) return;
        cloakComp.IsActive = false;

        if (!Deleted(cloakComp.EffectEntity))
            QueueDel(cloakComp.EffectEntity);

        _metaData.SetEntityName(uid, cloakComp.OriginalName);
        _movementSpeed.ChangeBaseSpeed(uid, cloakComp.OrigWalkSpeed, cloakComp.OrigSprintSpeed, cloakComp.OrigAcceleration);

        if (cloakComp.AddedStealth)
            RemComp<StealthComponent>(uid);
        else if (TryComp<StealthComponent>(uid, out var sc))
            _stealth.SetEnabled(uid, false, sc);

        if (cloakComp.DisableActionEntity.HasValue)
            _actions.RemoveAction(new Entity<ActionComponent?>(cloakComp.DisableActionEntity.Value, null));

        RemComp<HereticCloakActiveComponent>(uid);
    }

    private void OnHeartbeatMansus(EntityUid uid, HereticComponent comp, HereticHeartbeatMansusActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        TryReplaceHeartWithHereticHeart(uid);
        _heretic.OpenTargetBui(uid, comp);
    }

    private void TryReplaceHeartWithHereticHeart(EntityUid uid)
    {
        if (!_container.TryGetContainer(uid, BodyComponent.ContainerID, out var organs))
            return;

        foreach (var organ in organs.ContainedEntities)
        {
            if (HasComp<HereticHeartComponent>(organ))
                return;
        }

        EntityUid? oldHeart = null;
        foreach (var organ in organs.ContainedEntities.ToList())
        {
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } category)
                continue;
            if (category == "Heart")
            {
                oldHeart = organ;
                break;
            }
        }

        if (oldHeart.HasValue)
        {
            _container.Remove(oldHeart.Value, organs);
            Del(oldHeart.Value);
        }

        var newHeart = Spawn("OrganHereticHeart", Transform(uid).Coordinates);
        _container.Insert(newHeart, organs);
    }

    // ─── Special abilities ────────────────────────────────────────────────────

    private void OnRelentlessHeartbeat(EntityUid uid, HereticComponent comp, HereticRelentlessHeartbeatActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (!_mind.TryGetMind(uid, out var mindId, out _)) return;
        _heretic.AssignNamedTargets(uid, mindId);
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), Filter.Entities(uid), false);
        _popup.PopupEntity(Loc.GetString("heretic-relentless-heartbeat"), uid, uid, PopupType.Large);
    }

    private void OnSummonFamiliar(EntityUid uid, HereticComponent comp, HereticSummonFamiliarActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var familiarId = comp.CurrentPath switch
        {
            HereticPath.Ash    => "HereticFamiliarStalker",
            HereticPath.Void   => "HereticFamiliarStalker",
            HereticPath.Moon   => "HereticFamiliarAshWalker",
            HereticPath.Blade  => "HereticFamiliarProphet",
            HereticPath.Flesh  => "HereticFamiliarMoonMass",
            HereticPath.Rust   => "HereticFamiliarMoonMass",
            _                  => "HereticFamiliarStalker"
        };

        var pos = _xform.GetMapCoordinates(uid);
        EntityManager.SpawnEntity(familiarId, pos);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_castsummon.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-summon-familiar"), uid, uid, PopupType.Medium);
    }

    // ─── Unsealed Arts ────────────────────────────────────────────────────────

    private static readonly string[] PaintingEntities =
    [
        "HereticPaintingWeeping",
        "HereticPaintingBeauty",
        "HereticPaintingVines",
        "HereticPaintingRust",
        "HereticPaintingDesire",
    ];

    private void OnUnsealedArts(EntityUid uid, HereticComponent comp, HereticUnsealedArtsActionEvent args)
    {
        if (args.Handled) return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var coords = args.Target;
        var painting = PaintingEntities[_random.Next(PaintingEntities.Length)];
        Spawn(painting, coords);
        _audio.PlayPvs(new SoundCollectionSpecifier("storageRustle"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-unsealed-arts-placed"), uid, uid, PopupType.Medium);
    }
}
