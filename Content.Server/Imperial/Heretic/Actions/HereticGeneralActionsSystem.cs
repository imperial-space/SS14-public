using System.Linq;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic.Actions;

/// <summary>
/// Общие способности еретика, не привязанные к пути: Покров тьмы, Сердцебиение Мансуса, призыв фамильяра, Необузданное искусство.
/// </summary>
public sealed class HereticGeneralActionsSystem : EntitySystem
{
    [Dependency] private readonly Content.Server.Imperial.Helpers.Schedule.ImperialScheduleSystem _schedule = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private static readonly EntProtoId DisableCloakProto = "ActionHereticDisableCloak";

    private static readonly EntProtoId HereticEffectSmoke = "HereticEffectSmoke";
    private static readonly EntProtoId HereticCloakEffect = "HereticCloakEffect";
    private static readonly EntProtoId HereticCloakTrailEffect = "HereticCloakTrailEffect";
    private static readonly ProtoId<OrganCategoryPrototype> HeartOrganCategory = "Heart";
    private static readonly EntProtoId OrganHereticHeart = "OrganHereticHeart";

    private static readonly TimeSpan CloakOfShadowDelay = TimeSpan.FromMilliseconds(100);

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
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        const int durationSeconds = 180;
        args.Handled = true;

        var origWalk = 2.5f;
        var origSprint = 4.5f;
        var origAccel = 20f;
        if (TryComp<MovementSpeedModifierComponent>(uid, out var moveComp))
        {
            origWalk = moveComp.BaseWalkSpeed;
            origSprint = moveComp.BaseSprintSpeed;
            origAccel = moveComp.Acceleration;
        }
        var originalName = MetaData(uid).EntityName;

        // Effects: smoke + sound + popup
        Spawn(HereticEffectSmoke, Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-cloak-of-shadow"), uid, uid, PopupType.Medium);

        // Spawn persistent curse overlay parented to the player
        var effectUid = Spawn(HereticCloakEffect, Transform(uid).Coordinates);
        _xform.SetParent(effectUid, uid);

        // Rename player, apply 25% speed boost, hide original sprite
        _metaData.SetEntityName(uid, Loc.GetString("imperial-heretic-heretic-general-actions-system-1"));
        _movementSpeed.ChangeBaseSpeed(uid, origWalk, origSprint * 1.25f, origAccel);
        var addedStealth = !HasComp<StealthComponent>(uid);
        var stealthComp = EnsureComp<StealthComponent>(uid);
        _stealth.SetEnabled(uid, true, stealthComp);
        _stealth.SetVisibility(uid, -1f, stealthComp);

        // Store deactivation data in component
        var cloakComp = EnsureComp<HereticCloakActiveComponent>(uid);
        cloakComp.OrigWalkSpeed = origWalk;
        cloakComp.OrigSprintSpeed = origSprint;
        cloakComp.OrigAcceleration = origAccel;
        cloakComp.OriginalName = originalName;
        cloakComp.EffectEntity = effectUid;
        cloakComp.AddedStealth = addedStealth;
        cloakComp.IsActive = true;

        // Grant the manual-disable action
        EntityUid? disableAction = null;
        _actions.AddAction(uid, ref disableAction, DisableCloakProto);
        cloakComp.DisableActionEntity = disableAction;

        // Curse icon trail
        void SpawnTrail()
        {
            if (!cloakComp.IsActive || Deleted(uid))
                return;
            Spawn(HereticCloakTrailEffect, Transform(uid).Coordinates);
            _schedule.Schedule(CloakOfShadowDelay, SpawnTrail);
        }
        _schedule.Schedule(CloakOfShadowDelay, SpawnTrail);

        _schedule.Schedule(TimeSpan.FromSeconds(durationSeconds), () =>
        {
            if (Deleted(uid))
                return;
            if (!TryComp<HereticCloakActiveComponent>(uid, out var cc))
                return;
            if (!cc.IsActive)
                return;
            DeactivateCloak(uid, cc);
        });
    }

    private void OnDisableCloak(EntityUid uid, HereticComponent comp, HereticDisableCloakActionEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<HereticCloakActiveComponent>(uid, out var cloakComp))
            return;
        args.Handled = true;
        DeactivateCloak(uid, cloakComp);
    }

    private void DeactivateCloak(EntityUid uid, HereticCloakActiveComponent cloakComp)
    {
        if (!cloakComp.IsActive)
            return;
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
        if (args.Handled)
            return;
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
            if (category == HeartOrganCategory)
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

        var newHeart = Spawn(OrganHereticHeart, Transform(uid).Coordinates);
        _container.Insert(newHeart, organs);
    }

    // ─── Special abilities ────────────────────────────────────────────────────

    private void OnRelentlessHeartbeat(EntityUid uid, HereticComponent comp, HereticRelentlessHeartbeatActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return;
        _heretic.AssignNamedTargets(uid, mindId);
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), Filter.Entities(uid), false);
        _popup.PopupEntity(Loc.GetString("heretic-relentless-heartbeat"), uid, uid, PopupType.Large);
    }

    private void OnSummonFamiliar(EntityUid uid, HereticComponent comp, HereticSummonFamiliarActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var familiarId = comp.CurrentPath switch
        {
            HereticPath.Ash => "HereticFamiliarStalker",
            HereticPath.Void => "HereticFamiliarStalker",
            HereticPath.Moon => "HereticFamiliarAshWalker",
            HereticPath.Blade => "HereticFamiliarProphet",
            HereticPath.Flesh => "HereticFamiliarMoonMass",
            HereticPath.Rust => "HereticFamiliarMoonMass",
            _ => "HereticFamiliarStalker"
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
        if (args.Handled)
            return;
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
