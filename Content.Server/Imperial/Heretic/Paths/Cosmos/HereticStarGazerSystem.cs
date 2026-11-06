using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

public sealed class HereticStarGazerSystem : EntitySystem
{
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly SoundPathSpecifier ExpansionSound =
        new("/Audio/Imperial/heretic/sound_magic_cosmic_expansion.ogg");
    private static readonly SoundPathSpecifier StarBlastSound =
        new("/Audio/Imperial/heretic/sound_effects_magic_cosmic_energy.ogg");

    private static readonly SoundPathSpecifier BeamWindUpSound =
        new("/Audio/Imperial/heretic/beam_open.ogg");
    private static readonly SoundPathSpecifier BeamLoopSound =
        new("/Audio/Imperial/heretic/beam_loop_one.ogg");

    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    private static readonly EntProtoId HereticCosmicCarpet = "HereticCosmicCarpet";
    private static readonly EntProtoId StarMarkStatusEffect = "StarMarkStatusEffect";
    private static readonly EntProtoId ProjectileStarBlast = "ProjectileStarBlast";
    private static readonly EntProtoId HereticStarGazerChargeOrb = "HereticStarGazerChargeOrb";
    private static readonly EntProtoId HereticStarGazerBeamHead = "HereticStarGazerBeamHead";
    private static readonly EntProtoId HereticStarGazerBeamTile = "HereticStarGazerBeamTile";
    private static readonly EntProtoId Ash2 = "Ash";
    private static readonly EntProtoId HereticStarGazerBeamHeadPreview = "HereticStarGazerBeamHeadPreview";
    private static readonly EntProtoId HereticStarGazerBeamTilePreview = "HereticStarGazerBeamTilePreview";
    private static readonly EntProtoId HereticStarGazerBeamTailPreview = "HereticStarGazerBeamTailPreview";

    private static readonly TimeSpan PullVictimsDuration = TimeSpan.FromSeconds(30);

    private const float PullVictimsRange = 2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticStarGazerComponent, HereticStarGazerCosmicExpansionActionEvent>(OnCosmicExpansion);
        SubscribeLocalEvent<HereticStarGazerComponent, HereticStarGazerStarBlastActionEvent>(OnStarBlast);
        SubscribeLocalEvent<HereticStarGazerComponent, HereticStarGazerDeathGazeActionEvent>(OnDeathGaze);
        SubscribeLocalEvent<HereticStarGazerComponent, HereticStarGazerFindMasterActionEvent>(OnFindMaster);
        SubscribeLocalEvent<HereticStarGazerComponent, MobStateChangedEvent>(OnDied);
        SubscribeLocalEvent<HereticStarGazerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<HereticStarGazerProjectileComponent, StartCollideEvent>(OnProjectileCollide);
        SubscribeLocalEvent<HereticStarGazerProjectileComponent, EntityTerminatingEvent>(OnProjectileTerminating);
        SubscribeNetworkEvent<HereticStarGazerCursorUpdateEvent>(OnCursorUpdate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticStarGazerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Регенерация HP
            if (_timing.CurTime >= comp.NextHeal)
            {
                comp.NextHeal = _timing.CurTime + comp.HealInterval;
                var heal = new DamageSpecifier();
                heal.DamageDict[BluntDamageType] = FixedPoint2.New(-comp.HealAmount);
                _damage.TryChangeDamage(uid, heal, ignoreResistances: true);
            }

            // Фаза раскрутки (wind-up)
            if (comp.BeamWindUp)
            {
                var xform = Transform(uid);
                if (IsBeamCancelled(xform, comp))
                {
                    CancelBeam(uid, comp);
                    continue;
                }


                // t=2.2с: показываем предпросмотр луча без урона (как в BandaStation)
                if (!comp.BeamVisualShown && (comp.BeamWindUpEnd - _timing.CurTime).TotalSeconds <= 0.8)
                {
                    comp.BeamVisualShown = true;
                    ShowBeamPreview(uid, comp);
                }

                if (_timing.CurTime >= comp.BeamWindUpEnd)
                    StartBeamChanneling(uid, comp);
                continue;
            }

            // Фаза канала (channeling)
            if (comp.BeamChanneling)
            {
                var xform = Transform(uid);
                if (IsBeamCancelled(xform, comp))
                {
                    CancelBeam(uid, comp);
                    continue;
                }

                if (_timing.CurTime >= comp.NextBeamDmg)
                {
                    comp.NextBeamDmg = _timing.CurTime + comp.BeamDmgInterval;
                    FireBeamTick(uid, comp);
                }

                if (_timing.CurTime >= comp.BeamChannelEnd)
                    StopBeam(uid, comp);

                continue;
            }
        }

        // Трейл снаряда Star Blast
        var projQuery = EntityQueryEnumerator<HereticStarGazerProjectileComponent, TransformComponent>();
        while (projQuery.MoveNext(out var uid, out var proj, out var xform))
        {
            if (_timing.CurTime < proj.NextTrail)
                continue;

            proj.NextTrail = _timing.CurTime + proj.TrailInterval;
            var localPos = xform.LocalPosition;
            var snapped = new Vector2(MathF.Floor(localPos.X) + 0.5f, MathF.Floor(localPos.Y) + 0.5f);
            Spawn(HereticCosmicCarpet, new EntityCoordinates(xform.ParentUid, snapped));
        }
    }

    // ─── Cosmic Expansion ───────────────────────────────────────────────────────

    private void OnCosmicExpansion(EntityUid uid, HereticStarGazerComponent comp, HereticStarGazerCosmicExpansionActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var xform = Transform(uid);
        var parentUid = xform.ParentUid;
        var center = new Vector2(MathF.Floor(xform.LocalPosition.X) + 0.5f, MathF.Floor(xform.LocalPosition.Y) + 0.5f);

        for (var dx = -2; dx <= 2; dx++)
            for (var dy = -2; dy <= 2; dy++)
                Spawn(HereticCosmicCarpet, new EntityCoordinates(parentUid, center + new Vector2(dx, dy)));

        var coords = xform.Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.CosmicExpansionRange))
        {
            if (ent.Owner == uid)
                continue;
            _statusEffects.TrySetStatusEffectDuration(ent.Owner, StarMarkStatusEffect, comp.CosmicExpansionDuration);
        }

        _audio.PlayPvs(ExpansionSound, uid);
        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-cosmic-expansion"), uid, uid, PopupType.Medium);
    }

    // ─── Star Blast ─────────────────────────────────────────────────────────────

    private void OnStarBlast(EntityUid uid, HereticStarGazerComponent comp, HereticStarGazerStarBlastActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (comp.ActiveProjectile != null && !TerminatingOrDeleted(comp.ActiveProjectile.Value))
            DetonateStarBlast(uid, comp, args);
        else
            ShootStarBlast(uid, comp, args);
    }

    private void ShootStarBlast(EntityUid uid, HereticStarGazerComponent comp, HereticStarGazerStarBlastActionEvent args)
    {
        var originCoords = Transform(uid).Coordinates;
        var originWorldPos = _xform.GetWorldPosition(uid);
        var targetWorldPos = _xform.ToMapCoordinates(args.Target).Position;

        var direction = targetWorldPos - originWorldPos;
        if (direction.LengthSquared() < 0.001f)
            direction = new Vector2(1f, 0f);
        direction = Vector2.Normalize(direction);

        var projectile = Spawn(ProjectileStarBlast, originCoords);
        var projComp = EnsureComp<HereticStarGazerProjectileComponent>(projectile);
        projComp.Shooter = uid;

        if (TryComp<PhysicsComponent>(projectile, out var physics))
            _physics.SetLinearVelocity(projectile, direction * 8f, body: physics);

        comp.ActiveProjectile = projectile;
        comp.StarBlastAction = args.Action.Owner;

        _actions.SetUseDelay(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp), comp.ShootStarBlastDelay);
        _audio.PlayPvs(StarBlastSound, uid);
        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-star-blast-fired"), uid, uid, PopupType.Medium);
    }

    private void DetonateStarBlast(EntityUid uid, HereticStarGazerComponent comp, HereticStarGazerStarBlastActionEvent args)
    {
        var projUid = comp.ActiveProjectile!.Value;
        var projCoords = Transform(projUid).Coordinates;
        var hereticCoords = Transform(uid).Coordinates;

        PullVictims(uid, hereticCoords);
        _xform.SetCoordinates(uid, projCoords);
        _audio.PlayPvs(StarBlastSound, uid);
        PullVictims(uid, projCoords);

        QueueDel(projUid);
        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-star-blast-detonated"), uid, uid, PopupType.Large);
    }

    private void PullVictims(EntityUid uid, EntityCoordinates coords)
    {
        var localPos = coords.Position;
        var snapped = new Vector2(MathF.Floor(localPos.X) + 0.5f, MathF.Floor(localPos.Y) + 0.5f);
        var parentUid = coords.EntityId;

        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                Spawn(HereticCosmicCarpet, new EntityCoordinates(parentUid, snapped + new Vector2(dx, dy)));

        var hereticWorldPos = _xform.GetWorldPosition(uid);

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, PullVictimsRange))
        {
            if (ent.Owner == uid)
                continue;
            _statusEffects.TrySetStatusEffectDuration(ent.Owner, StarMarkStatusEffect, PullVictimsDuration);

            if (!TryComp<PhysicsComponent>(ent.Owner, out var mobPhysics))
                continue;
            var mobPos = _xform.GetWorldPosition(ent.Owner);
            var delta = hereticWorldPos - mobPos;
            if (delta.LengthSquared() > 0.01f)
                _physics.SetLinearVelocity(ent.Owner, Vector2.Normalize(delta) * 6f, body: mobPhysics);
        }
    }

    private void OnProjectileCollide(EntityUid uid, HereticStarGazerProjectileComponent comp, ref StartCollideEvent args)
    {
        switch (args.OurFixtureId)
        {
            case "mob_sensor":
                if (args.OtherEntity == comp.Shooter)
                    return;
                if (!HasComp<MobStateComponent>(args.OtherEntity))
                    return;

                _stun.TryKnockdown(args.OtherEntity, comp.ProjectileCollideMobSensorTime, true);

                var coords = Transform(uid).Coordinates;
                foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, comp.ProjectileCollideMobSensorRange))
                {
                    if (ent.Owner == comp.Shooter)
                        continue;
                    _statusEffects.TrySetStatusEffectDuration(ent.Owner, StarMarkStatusEffect, comp.ProjectileCollideMobSensorDuration);
                }

                QueueDel(uid);
                break;

            case "wall_stop":
                QueueDel(uid);
                break;
        }
    }

    private void OnProjectileTerminating(EntityUid uid, HereticStarGazerProjectileComponent comp, ref EntityTerminatingEvent args)
    {
        if (!TryComp<HereticStarGazerComponent>(comp.Shooter, out var gazer))
            return;
        if (gazer.ActiveProjectile != uid)
            return;

        gazer.ActiveProjectile = null;

        if (gazer.StarBlastAction != null)
        {
            var actionEnt = new Entity<ActionComponent?>(gazer.StarBlastAction.Value, null);
            _actions.SetUseDelay(actionEnt, comp.ProjectileTerminatingDelay);
            _actions.SetCooldown(actionEnt, comp.ProjectileTerminatingCooldown);
        }
    }

    // ─── Death Gaze (Звёздный взгляд) ──────────────────────────────────────────

    private void OnDeathGaze(EntityUid uid, HereticStarGazerComponent comp, HereticStarGazerDeathGazeActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        // Уже активен — игнорируем
        if (comp.BeamWindUp || comp.BeamChanneling)
            return;

        var xform = Transform(uid);
        comp.BeamWindUp = true;
        Dirty(uid, comp);
        comp.BeamWindUpEnd = _timing.CurTime + comp.BeamWindUpDuration;
        comp.BeamVisualShown = false;
        comp.BeamDeathGazeAction = args.Action.Owner;
        comp.BeamStartPos = xform.LocalPosition;
        comp.NextBeamDmg = TimeSpan.Zero;

        // Направление — от гейзера к курсору
        var originWorldPos = _xform.GetWorldPosition(uid);
        var targetWorldPos = _xform.ToMapCoordinates(args.Target).Position;
        var delta = targetWorldPos - originWorldPos;
        comp.BeamStartRot = delta.LengthSquared() > 0.001f
            ? new Angle(delta)
            : new Angle(0);

        // Орб — 1 тайл вперёд от гейзера (как в BandaStation)
        var orbOffset = comp.BeamStartRot.ToVec();
        comp.BeamChargeOrb = Spawn(HereticStarGazerChargeOrb,
            new EntityCoordinates(xform.ParentUid, xform.LocalPosition + orbOffset));

        _audio.PlayPvs(BeamWindUpSound, uid);
        _movement.RefreshMovementSpeedModifiers(uid);

        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-beam-windup"), uid, uid, PopupType.Large);
    }

    private void OnRefreshMovespeed(EntityUid uid, HereticStarGazerComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.BeamWindUp || comp.BeamChanneling)
            args.ModifySpeed(0.5f, 0.5f);
    }

    private void OnCursorUpdate(HereticStarGazerCursorUpdateEvent msg)
    {
        if (!TryGetEntity(msg.Uid, out var uid))
            return;
        if (!TryComp<HereticStarGazerComponent>(uid, out var comp))
            return;
        comp.BeamCursorPos = msg.Coords;
    }

    /// <summary>Возвращает true, если Созерцатель сдвинулся дальше порога.</summary>
    private static bool IsBeamCancelled(TransformComponent xform, HereticStarGazerComponent comp)
    {
        return Vector2.Distance(xform.LocalPosition, comp.BeamStartPos) > 0.5f;
    }

    private void StartBeamChanneling(EntityUid uid, HereticStarGazerComponent comp)
    {
        if (comp.BeamChargeOrb.HasValue && !TerminatingOrDeleted(comp.BeamChargeOrb.Value))
            QueueDel(comp.BeamChargeOrb.Value);
        comp.BeamChargeOrb = null;

        comp.BeamWindUp = false;
        comp.BeamChanneling = true;
        Dirty(uid, comp);
        comp.BeamChannelEnd = _timing.CurTime + comp.BeamChannelDuration;
        comp.NextBeamDmg = TimeSpan.Zero;

        _audio.PlayPvs(BeamLoopSound, uid);
        _movement.RefreshMovementSpeedModifiers(uid);

        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-beam-firing"), uid, uid, PopupType.Large);
    }

    private void CancelBeam(EntityUid uid, HereticStarGazerComponent comp)
    {
        var wasWindingUp = comp.BeamWindUp;

        if (comp.BeamChargeOrb.HasValue && !TerminatingOrDeleted(comp.BeamChargeOrb.Value))
            QueueDel(comp.BeamChargeOrb.Value);
        comp.BeamChargeOrb = null;
        comp.BeamWindUp = false;
        comp.BeamChanneling = false;
        Dirty(uid, comp);

        // Прерван во время раскрутки — кулдаун сбрасывается до 1с (как в BandaStation)
        if (wasWindingUp && comp.BeamDeathGazeAction.HasValue)
        {
            var actionEnt = new Entity<ActionComponent?>(comp.BeamDeathGazeAction.Value, null);
            _actions.SetUseDelay(actionEnt, comp.CancelBeamDelay);
            _actions.SetCooldown(actionEnt, comp.CancelBeamCooldown);
        }

        _movement.RefreshMovementSpeedModifiers(uid);
        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-beam-cancelled"), uid, uid, PopupType.SmallCaution);
    }

    private void StopBeam(EntityUid uid, HereticStarGazerComponent comp)
    {
        comp.BeamChanneling = false;
        Dirty(uid, comp);
        _movement.RefreshMovementSpeedModifiers(uid);
        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-beam-done"), uid, uid, PopupType.Medium);
    }

    /// <summary>Один тик луча: спавн визуала, урон, гравитационное притяжение.</summary>
    private void FireBeamTick(EntityUid uid, HereticStarGazerComponent comp)
    {
        // Обновляем направление луча из последней позиции курсора
        var worldPos = _xform.GetWorldPosition(uid);
        if (comp.BeamCursorPos.HasValue)
        {
            var cursorDelta = comp.BeamCursorPos.Value.Position - worldPos;
            if (cursorDelta.LengthSquared() > 0.001f)
                comp.BeamStartRot = new Angle(cursorDelta);
        }

        var worldRot = comp.BeamStartRot;
        var mapId = Transform(uid).MapID;

        var forward = worldRot.ToVec();
        var perp = new Angle(worldRot.Theta + Math.PI / 2.0).ToVec();

        // ── Визуалы ────────────────────────────────────────────────────────────

        // Голова — 2 тайла вперёд
        var headEnt = Spawn(HereticStarGazerBeamHead,
            new MapCoordinates(worldPos + forward * 2.0f, mapId));
        _xform.SetWorldRotation(headEnt, worldRot);

        // Заполнение — тайлы 3..BeamRange-2; 2% гравитация per entity
        for (var step = 3; step < comp.BeamRange - 1; step++)
        {
            var tileCenter = worldPos + forward * (step + 0.5f);
            var fillEnt = Spawn(HereticStarGazerBeamTile,
                new MapCoordinates(tileCenter, mapId));
            _xform.SetWorldRotation(fillEnt, new Angle(worldRot.Theta + Math.PI / 2));

            if (_random.Prob(comp.FireBeamTickChance))
                PullTowards(uid, comp, tileCenter, mapId);
        }

        // Хвост — в конце жизни луча показываем анимацию закрытия
        var tailCenter = worldPos + forward * (comp.BeamRange - 0.5f);
        var tailId = (comp.BeamChannelEnd - _timing.CurTime).TotalSeconds <= 0.8 ? "HereticStarGazerBeamTileClosing" : "HereticStarGazerBeamTail";
        var tailEnt = Spawn(tailId, new MapCoordinates(tailCenter, mapId));
        _xform.SetWorldRotation(tailEnt, new Angle(worldRot.Theta + Math.PI / 2));

        // ── Урон (все тайлы 0..BeamRange-1, три полосы) ───────────────────────

        var burnDmg = new DamageSpecifier(comp.BurnDamage);

        var wallDmg = new DamageSpecifier(comp.WallDamage);

        var ashSet = new HashSet<EntityUid>();

        for (var step = 0; step < comp.BeamRange; step++)
        {
            for (var side = -1; side <= 1; side++)
            {
                var tileCenter = worldPos + forward * (step + 0.5f) + perp * side;
                var mapCoords = new MapCoordinates(tileCenter, mapId);

                foreach (var ent in _lookup.GetEntitiesInRange<DamageableComponent>(mapCoords, comp.FireBeamTickRange))
                {
                    if (ent.Owner == uid)
                        continue;
                    if (comp.Master.HasValue && ent.Owner == comp.Master.Value)
                        continue;

                    if (TryComp<MobStateComponent>(ent.Owner, out var mobState))
                    {
                        if (mobState.CurrentState == MobState.Critical ||
                            mobState.CurrentState == MobState.Dead)
                        {
                            if (ashSet.Add(ent.Owner))
                            {
                                Spawn(Ash2, Transform(ent.Owner).Coordinates);
                                _popup.PopupEntity(
                                    Loc.GetString("heretic-star-gazer-beam-ash"),
                                    ent.Owner, PopupType.LargeCaution);
                                QueueDel(ent.Owner);
                            }
                        }
                        else
                        {
                            _damage.TryChangeDamage(ent.Owner, burnDmg, ignoreResistances: false);
                        }
                    }
                    else
                    {
                        _damage.TryChangeDamage(ent.Owner, wallDmg, ignoreResistances: true);
                    }
                }
            }
        }
    }

    /// <summary>Показывает предпросмотр луча (визуал без урона) — t=2.2с wind-up.</summary>
    private void ShowBeamPreview(EntityUid uid, HereticStarGazerComponent comp)
    {
        // Орб исчезает при появлении луча (как в BandaStation open_laser)
        if (comp.BeamChargeOrb.HasValue && !TerminatingOrDeleted(comp.BeamChargeOrb.Value))
            QueueDel(comp.BeamChargeOrb.Value);
        comp.BeamChargeOrb = null;

        // Обновляем направление из курсора перед показом preview
        var worldPos = _xform.GetWorldPosition(uid);
        if (comp.BeamCursorPos.HasValue)
        {
            var cursorDelta = comp.BeamCursorPos.Value.Position - worldPos;
            if (cursorDelta.LengthSquared() > 0.001f)
                comp.BeamStartRot = new Angle(cursorDelta);
        }

        var worldRot = comp.BeamStartRot;
        var mapId = Transform(uid).MapID;

        var forward = worldRot.ToVec();

        var headEnt = Spawn(HereticStarGazerBeamHeadPreview,
            new MapCoordinates(worldPos + forward * 2.0f, mapId));
        _xform.SetWorldRotation(headEnt, worldRot);

        for (var step = 3; step < comp.BeamRange - 1; step++)
        {
            var tileCenter = worldPos + forward * (step + 0.5f);
            var fillEnt = Spawn(HereticStarGazerBeamTilePreview,
                new MapCoordinates(tileCenter, mapId));
            _xform.SetWorldRotation(fillEnt, new Angle(worldRot.Theta + Math.PI / 2));
        }

        var tailPreviewCenter = worldPos + forward * (comp.BeamRange - 0.5f);
        var tailPreviewEnt = Spawn(HereticStarGazerBeamTailPreview,
            new MapCoordinates(tailPreviewCenter, mapId));
        _xform.SetWorldRotation(tailPreviewEnt, new Angle(worldRot.Theta + Math.PI / 2));
    }

    /// <summary>Притягивает мобов в радиусе 5 тайлов к точке point (2% chance per tile).</summary>
    private void PullTowards(EntityUid uid, HereticStarGazerComponent comp, Vector2 point, MapId mapId)
    {
        var mapCoords = new MapCoordinates(point, mapId);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(mapCoords, comp.PullTowardsRange))
        {
            if (ent.Owner == uid)
                continue;
            if (comp.Master.HasValue && ent.Owner == comp.Master.Value)
                continue;
            if (!TryComp<PhysicsComponent>(ent.Owner, out var physics))
                continue;

            var mobPos = _xform.GetWorldPosition(ent.Owner);
            var delta = point - mobPos;
            if (delta.LengthSquared() > 0.01f)
                _physics.SetLinearVelocity(ent.Owner, Vector2.Normalize(delta) * 8f, body: physics);
        }
    }

    // ─── Find Master ────────────────────────────────────────────────────────────

    private void OnFindMaster(EntityUid uid, HereticStarGazerComponent comp, HereticStarGazerFindMasterActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (comp.Master == null || TerminatingOrDeleted(comp.Master.Value))
        {
            _popup.PopupEntity(Loc.GetString("heretic-star-gazer-no-master"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var masterCoords = Transform(comp.Master.Value).Coordinates;
        _xform.SetCoordinates(uid, masterCoords);
        _audio.PlayPvs(StarBlastSound, uid);
        _popup.PopupEntity(Loc.GetString("heretic-star-gazer-find-master"), uid, uid, PopupType.Medium);
    }

    // ─── Death Link ─────────────────────────────────────────────────────────────

    private void OnDied(EntityUid uid, HereticStarGazerComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        if (comp.Master == null || TerminatingOrDeleted(comp.Master.Value))
            return;

        // Мастер получает 200 Brute при гибели Созерцателя
        var dmg = new DamageSpecifier(comp.DiedBluntDamage);
        _damage.TryChangeDamage(comp.Master.Value, dmg, ignoreResistances: false);
    }
}
