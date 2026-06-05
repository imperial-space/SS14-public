using Content.Server.Popups;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Imperial.Lavaland.Bubblegum;

public sealed class BubblegumSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private readonly Dictionary<EntityUid, List<PendingTileDamage>> _pendingDamage = new();

    private readonly record struct PendingTileDamage(EntityCoordinates Tile, TimeSpan TriggerTime, float Damage);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BubblegumComponent, DamageableComponent, NpcFactionMemberComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damageable, out var faction, out var state))
        {
            if (state.CurrentState != MobState.Alive)
                continue;

            var now = _timing.CurTime;

            ProcessPendingTileDamage(uid);

            var totalDamage = _damageable.GetPositiveDamage((uid, damageable)).GetTotal().Float();
            var healthRatio = totalDamage / comp.MaxHp; // 0 = full HP, 1 = dead

            // 1. Rage end
            if (comp.IsRaging && now >= comp.RageEndTime)
                EndRage(uid, comp);

            // 2. Permanent enrage at 50% HP taken (one-time)
            if (!comp.IsEnraged && healthRatio >= 0.5f)
                TriggerEnrage(uid, comp);

            // 3. Blood trail — spawn hazard tile and track it for Blood Dive
            if (now >= comp.NextBloodTrailTime)
            {
                var tileCoords = SnapToTile(Transform(uid).Coordinates);
                if (TrySpawnBloodPuddle(uid, comp, out var puddleUid))
                    comp.BloodTileList.Add(puddleUid);
                comp.BloodTileList.RemoveAll(t => !Exists(t));
                QueueTileDamage(uid, tileCoords, comp.BloodTileDamage, comp.BloodTileDelay);
                comp.NextBloodTrailTime = now + TimeSpan.FromSeconds(comp.BloodTrailCooldown);
            }

            // 4. Blood hands (passive, independent of other states)
            ProcessBloodHands(uid, comp);

            // 5. Active hallu state — highest priority movement lock
            if (comp.IsHalluActive)
            {
                ProcessHallu(uid, comp, frameTime);
                continue;
            }

            // 6. Active dash state
            if (comp.IsDashWaiting || comp.IsDashMoving || comp.IsDashPausing)
            {
                ProcessDash(uid, comp, frameTime);
                continue;
            }

            // --- Idle decisions ---

            // 7. Rage trigger
            if (!comp.IsRaging && now >= comp.NextRageTime && TryFindNearbyPlayer(uid, comp.TargetSearchRange, out _))
                TriggerRage(uid, comp, healthRatio);

            // 8. Blood Dive
            if (now >= comp.NextDiveTime &&
                comp.BloodTileList.Count > 0 &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var diveTarget))
                TryDoBloodDive(uid, comp, diveTarget);

            // 9. Triple Dash
            if (now >= comp.NextDashTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var dashTarget))
                StartDashSequence(uid, comp, dashTarget);

            // 10. Hallucination attacks (< 50% HP)
            if (healthRatio >= 0.5f)
            {
                if (now >= comp.NextHalluDashTime &&
                    TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var ht1))
                    StartHalluDash(uid, comp, ht1, 0);
                else if (now >= comp.NextCircleHalluTime &&
                    TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var ht2))
                    StartHalluDash(uid, comp, ht2, 1);
                else if (now >= comp.NextRandomHalluTime &&
                    TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var ht3))
                    StartHalluDash(uid, comp, ht3, 2);
            }

            // 11. Blood blast (melee-counter)
            if (comp.BlastReady && now >= comp.NextBlastTime &&
                TryFindNearbyHostile((uid, faction), comp.TargetSearchRange, out var blastTarget))
            {
                if (Transform(uid).Coordinates.TryDistance(EntityManager,
                    Transform(blastTarget).Coordinates, out var bd) && bd <= comp.BlastRange)
                {
                    DoBloodBlast(uid, Transform(blastTarget).Coordinates, comp);
                    comp.MeleeHits = 0;
                    comp.BlastReady = false;
                    comp.NextBlastTime = now + TimeSpan.FromSeconds(comp.BlastCooldown);
                }
            }

            // 12. Clone assault (< 50% HP)
            if (healthRatio >= 0.5f && now >= comp.NextCloneTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var cloneTarget))
                DoCloneAssault(uid, comp, cloneTarget);
        }
    }

    // ── Melee hit handler ─────────────────────────────────────────────────────

    private void OnMeleeHit(MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0 ||
            !TryComp<BubblegumComponent>(args.User, out var comp))
            return;

        if (TryComp<MobStateComponent>(args.User, out var myState) &&
            myState.CurrentState != MobState.Alive)
            return;

        comp.MeleeHits++;
        if (comp.MeleeHits >= comp.MeleeHitsBeforeBlast)
            comp.BlastReady = true;

        foreach (var hit in args.HitEntities)
        {
            if (!TryComp<MobStateComponent>(hit, out var hitState) ||
                hitState.CurrentState != MobState.Critical)
                continue;

            if (!TryComp<DamageableComponent>(hit, out var hitDamageable))
                continue;

            var devourSpec = new DamageSpecifier();
            devourSpec.DamageDict.Add("Blunt", FixedPoint2.New(comp.DevourDamage));
            _damageable.TryChangeDamage((hit, hitDamageable), devourSpec, origin: args.User);
        }
    }

    // ── Rage ──────────────────────────────────────────────────────────────────

    private void TriggerRage(EntityUid uid, BubblegumComponent comp, float healthRatio)
    {
        comp.IsRaging = true;
        var duration = comp.RageMinDuration + healthRatio * (comp.RageMaxDuration - comp.RageMinDuration);
        comp.RageEndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);
        comp.NextRageTime = comp.RageEndTime + TimeSpan.FromSeconds(comp.RageCooldown);

        EnsureComp<GodmodeComponent>(uid);
        _appearance.SetData(uid, BubblegumVisuals.Raging, true);
        _audio.PlayPvs(comp.RageSound, uid);

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } player) continue;
            if (!Transform(uid).Coordinates.TryDistance(EntityManager,
                Transform(player).Coordinates, out var dist) || dist > 20f) continue;
            _popup.PopupEntity(Loc.GetString("bubblegum-rage-message"), uid, player,
                PopupType.LargeCaution);
        }
    }

    private void EndRage(EntityUid uid, BubblegumComponent comp)
    {
        comp.IsRaging = false;
        RemComp<GodmodeComponent>(uid);
        _appearance.SetData(uid, BubblegumVisuals.Raging, false);
    }

    // ── Blood Dive ────────────────────────────────────────────────────────────

    private void TryDoBloodDive(EntityUid uid, BubblegumComponent comp, EntityUid player)
    {
        comp.BloodTileList.RemoveAll(t => !Exists(t));

        var playerCoords = Transform(player).Coordinates;
        var validTiles = new List<EntityCoordinates>();

        foreach (var tileUid in comp.BloodTileList)
        {
            if (!Exists(tileUid)) continue;
            var tc = Transform(tileUid).Coordinates;
            if (!playerCoords.TryDistance(EntityManager, tc, out var dist)) continue;
            if (dist < comp.DiveMinDistFromPlayer || dist > comp.DiveMaxDistFromPlayer) continue;
            validTiles.Add(tc);
        }

        if (validTiles.Count == 0)
            return;

        var target = _random.Pick(validTiles);
        _transform.SetCoordinates(uid, target);
        comp.NextDiveTime = _timing.CurTime + TimeSpan.FromSeconds(comp.DiveCooldown);
        _audio.PlayPvs(comp.DiveSound, uid);

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } p) continue;
            if (!playerCoords.TryDistance(EntityManager, Transform(p).Coordinates, out var d) || d > 20f) continue;
            _popup.PopupEntity(Loc.GetString("bubblegum-dive-message"), uid, p, PopupType.Medium);
        }
    }

    // ── Blood Hands ───────────────────────────────────────────────────────────

    private void ProcessBloodHands(EntityUid uid, BubblegumComponent comp)
    {
        var now = _timing.CurTime;

        // Trigger damage for hands that are ready
        for (var i = comp.ActiveBloodHands.Count - 1; i >= 0; i--)
        {
            var (handUid, triggerTime) = comp.ActiveBloodHands[i];
            if (!Exists(handUid))
            {
                comp.ActiveBloodHands.RemoveAt(i);
                continue;
            }

            if (now < triggerTime)
                continue;

            DamagePlayersNear(uid, Transform(handUid).Coordinates, comp.HandDamage, 1.5f);
            comp.ActiveBloodHands.RemoveAt(i);
        }

        // Spawn new hand
        if (now < comp.NextHandTime)
            return;

        comp.NextHandTime = now + TimeSpan.FromSeconds(comp.HandSpawnInterval);

        if (!_random.Prob(comp.HandChance))
            return;

        if (!TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var handTarget))
            return;

        var handCoords = Transform(handTarget).Coordinates;
        var hand = Spawn(comp.HandPrototype, handCoords);
        comp.ActiveBloodHands.Add((hand, now + TimeSpan.FromSeconds(comp.HandDelay)));
    }

    // ── Triple Dash ───────────────────────────────────────────────────────────

    private void StartDashSequence(EntityUid uid, BubblegumComponent comp, EntityUid target)
    {
        comp.DashTargetPlayer = target;
        comp.DashLegIndex = 0;
        comp.IsDashWaiting = false;
        comp.IsDashMoving = false;
        comp.IsDashPausing = false;
        StartDashLeg(uid, comp);
    }

    private void StartDashLeg(EntityUid uid, BubblegumComponent comp)
    {
        if (!Exists(comp.DashTargetPlayer) ||
            !TryComp<MobStateComponent>(comp.DashTargetPlayer, out var ts) ||
            ts.CurrentState != MobState.Alive)
        {
            FinishDash(comp);
            return;
        }

        var bossCoords = Transform(uid).Coordinates;
        var playerCoords = Transform(comp.DashTargetPlayer).Coordinates;

        if (!bossCoords.TryDistance(EntityManager, playerCoords, out var d) || d < 0.1f)
        {
            FinishDash(comp);
            return;
        }

        // Marker behind player (away from boss)
        var dir = new Vector2(playerCoords.X - bossCoords.X, playerCoords.Y - bossCoords.Y) / d;
        comp.DashMarkerPos = playerCoords.Offset(dir * comp.DashMarkerBackOffset);
        Spawn(comp.DashMarkerPrototype, comp.DashMarkerPos);
        _audio.PlayPvs(comp.DashSound, uid);

        comp.IsDashWaiting = true;
        comp.DashWaitEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.DashLegWaits[comp.DashLegIndex]);

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } p) continue;
            if (!bossCoords.TryDistance(EntityManager, Transform(p).Coordinates, out var pd) || pd > 22f) continue;
            _popup.PopupEntity(Loc.GetString("bubblegum-dash-message"), uid, p, PopupType.LargeCaution);
        }
    }

    private void ProcessDash(EntityUid uid, BubblegumComponent comp, float frameTime)
    {
        var now = _timing.CurTime;

        if (comp.IsDashWaiting)
        {
            if (now >= comp.DashWaitEndTime)
            {
                comp.IsDashWaiting = false;
                comp.IsDashMoving = true;
                comp.DashMoveEndTime = now + TimeSpan.FromSeconds(comp.DashMoveDuration);
                comp.LastTrailTime = now;
            }
            return;
        }

        if (comp.IsDashMoving)
        {
            // Spawn trail sprite
            if (now >= comp.LastTrailTime + TimeSpan.FromSeconds(comp.DashTrailInterval))
            {
                Spawn(comp.DashTrailPrototype, Transform(uid).Coordinates);
                comp.LastTrailTime = now;
            }

            var bossCoords = Transform(uid).Coordinates;
            var dest = comp.DashMarkerPos;
            var arrived = false;

            if (bossCoords.TryDistance(EntityManager, dest, out var dist) && dist < 0.4f)
                arrived = true;

            if (!arrived && now < comp.DashMoveEndTime && dist > 0.1f)
            {
                var dir = new Vector2(dest.X - bossCoords.X, dest.Y - bossCoords.Y) / dist;
                var step = MathF.Min(comp.DashSpeed * frameTime, dist - 0.2f);
                _transform.SetCoordinates(uid, bossCoords.Offset(dir * step));
            }
            else if (arrived || now >= comp.DashMoveEndTime)
            {
                DamagePlayersNear(uid, Transform(uid).Coordinates, comp.DashDamage, comp.DashDamageRadius);
                comp.IsDashMoving = false;

                if (comp.DashLegIndex + 1 < comp.DashLegWaits.Count)
                {
                    comp.IsDashPausing = true;
                    comp.DashPauseEndTime = now + TimeSpan.FromSeconds(comp.DashLegPause);
                }
                else
                {
                    FinishDash(comp);
                }
            }
            return;
        }

        if (comp.IsDashPausing && now >= comp.DashPauseEndTime)
        {
            comp.IsDashPausing = false;
            comp.DashLegIndex++;
            StartDashLeg(uid, comp);
        }
    }

    private void FinishDash(BubblegumComponent comp)
    {
        comp.IsDashWaiting = false;
        comp.IsDashMoving = false;
        comp.IsDashPausing = false;
        comp.NextDashTime = _timing.CurTime + TimeSpan.FromSeconds(comp.DashCooldown);
    }

    // ── Hallucination Dash ────────────────────────────────────────────────────

    private void StartHalluDash(EntityUid uid, BubblegumComponent comp, EntityUid target, int variant)
    {
        comp.IsHalluActive = true;
        comp.HalluVariant = variant;
        comp.HalluLegIndex = 0;
        comp.HalluNeedsNormalDash = variant == 0;
        comp.HalluCircleAngle = _random.NextFloat(0f, MathF.PI * 2f);
        StartHalluLeg(uid, comp, target);
    }

    private void StartHalluLeg(EntityUid uid, BubblegumComponent comp, EntityUid target)
    {
        if (!Exists(target) ||
            !TryComp<MobStateComponent>(target, out var ts) ||
            ts.CurrentState != MobState.Alive)
        {
            CompleteHalluAttack(uid, comp);
            return;
        }

        var playerCoords = Transform(target).Coordinates;
        comp.HalluMarkerPos = playerCoords;

        Spawn(comp.DashMarkerPrototype, playerCoords);
        SpawnPhantoms(comp, playerCoords);

        comp.IsHalluWaiting = true;
        comp.IsHalluMoving = false;
        comp.IsHalluPausing = false;
        comp.HalluWaitEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.PhantomWaitDuration);

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } p) continue;
            if (!playerCoords.TryDistance(EntityManager, Transform(p).Coordinates, out var pd) || pd > 22f) continue;
            _popup.PopupEntity(Loc.GetString("bubblegum-hallu-message"), uid, p, PopupType.LargeCaution);
        }
    }

    private void SpawnPhantoms(BubblegumComponent comp, EntityCoordinates markerPos)
    {
        foreach (var (ph, _) in comp.ActivePhantoms)
            if (Exists(ph)) QueueDel(ph);
        comp.ActivePhantoms.Clear();

        foreach (var offset in GetPhantomOffsets(comp))
        {
            var phantom = Spawn(comp.PhantomPrototype, markerPos.Offset(offset));
            comp.ActivePhantoms.Add((phantom, markerPos));
        }
    }

    private List<Vector2> GetPhantomOffsets(BubblegumComponent comp)
    {
        var r = comp.PhantomRadius;
        return comp.HalluVariant switch
        {
            1 => GetCircleOffsets(5, r, comp.HalluCircleAngle),
            2 => GetRandomOffsets(4, r),
            _ => _random.Prob(0.5f)
                    ? new List<Vector2>
                    {
                        new(r, 0), new(-r, 0), new(0, r), new(0, -r)
                    }
                    : new List<Vector2>
                    {
                        new( r * 0.707f,  r * 0.707f),
                        new(-r * 0.707f,  r * 0.707f),
                        new( r * 0.707f, -r * 0.707f),
                        new(-r * 0.707f, -r * 0.707f)
                    }
        };
    }

    private static List<Vector2> GetCircleOffsets(int count, float radius, float startAngle)
    {
        var list = new List<Vector2>(count);
        for (var i = 0; i < count; i++)
        {
            var angle = startAngle + MathF.PI * 2f / count * i;
            list.Add(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
        return list;
    }

    private List<Vector2> GetRandomOffsets(int count, float radius)
    {
        var list = new List<Vector2>(count);
        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextFloat(0f, MathF.PI * 2f);
            var r = _random.NextFloat(radius * 0.6f, radius * 1.4f);
            list.Add(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r);
        }
        return list;
    }

    private void ProcessHallu(EntityUid uid, BubblegumComponent comp, float frameTime)
    {
        var now = _timing.CurTime;

        if (comp.IsHalluWaiting)
        {
            if (now >= comp.HalluWaitEndTime)
            {
                comp.IsHalluWaiting = false;
                comp.IsHalluMoving = true;
                comp.HalluMoveEndTime = now + TimeSpan.FromSeconds(comp.PhantomDashDuration);
            }
            return;
        }

        if (comp.IsHalluMoving)
        {
            var allDone = true;
            for (var i = comp.ActivePhantoms.Count - 1; i >= 0; i--)
            {
                var (phantom, target) = comp.ActivePhantoms[i];
                if (!Exists(phantom))
                {
                    comp.ActivePhantoms.RemoveAt(i);
                    continue;
                }

                var pCoords = Transform(phantom).Coordinates;
                if (!pCoords.TryDistance(EntityManager, target, out var dist) || dist < 0.35f)
                {
                    DamagePlayersNear(uid, pCoords, comp.PhantomDamage, comp.PhantomDamageRadius);
                    QueueDel(phantom);
                    comp.ActivePhantoms.RemoveAt(i);
                    continue;
                }

                allDone = false;
                var direction = new Vector2(target.X - pCoords.X, target.Y - pCoords.Y) / dist;
                var step = MathF.Min(comp.DashSpeed * frameTime, dist - 0.2f);
                _transform.SetCoordinates(phantom, pCoords.Offset(direction * step));
            }

            if (allDone || now >= comp.HalluMoveEndTime)
            {
                foreach (var (ph, _) in comp.ActivePhantoms)
                    if (Exists(ph)) QueueDel(ph);
                comp.ActivePhantoms.Clear();

                comp.IsHalluMoving = false;
                comp.HalluLegIndex++;

                if (comp.HalluLegIndex < comp.HalluLegCount)
                {
                    comp.IsHalluPausing = true;
                    comp.HalluPauseEndTime = now + TimeSpan.FromSeconds(comp.HalluLegPause);
                    comp.HalluCircleAngle += MathF.PI * 2f / (comp.HalluLegCount * 5);
                }
                else
                {
                    CompleteHalluAttack(uid, comp);
                }
            }
            return;
        }

        if (comp.IsHalluPausing && now >= comp.HalluPauseEndTime)
        {
            comp.IsHalluPausing = false;
            if (TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var nextTarget))
                StartHalluLeg(uid, comp, nextTarget);
            else
                CompleteHalluAttack(uid, comp);
        }
    }

    private void CompleteHalluAttack(EntityUid uid, BubblegumComponent comp)
    {
        comp.IsHalluActive = false;
        comp.IsHalluWaiting = false;
        comp.IsHalluMoving = false;
        comp.IsHalluPausing = false;

        foreach (var (ph, _) in comp.ActivePhantoms)
            if (Exists(ph)) QueueDel(ph);
        comp.ActivePhantoms.Clear();

        var now = _timing.CurTime;
        switch (comp.HalluVariant)
        {
            case 0:
                comp.NextHalluDashTime = now + TimeSpan.FromSeconds(comp.HalluDashCooldown);
                if (comp.HalluNeedsNormalDash &&
                    TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var dashTarget))
                    StartDashSequence(uid, comp, dashTarget);
                break;
            case 1:
                comp.NextCircleHalluTime = now + TimeSpan.FromSeconds(comp.CircleHalluCooldown);
                break;
            case 2:
                comp.NextRandomHalluTime = now + TimeSpan.FromSeconds(comp.RandomHalluCooldown);
                break;
        }
    }

    // ── Enrage ────────────────────────────────────────────────────────────────

    private void TriggerEnrage(EntityUid uid, BubblegumComponent comp)
    {
        comp.IsEnraged = true;
        _audio.PlayPvs(comp.EnrageSound, uid);

        if (!TryComp<MovementSpeedModifierComponent>(uid, out var moveComp))
            return;

        var newSpeed = MathF.Max(moveComp.BaseWalkSpeed, moveComp.BaseWalkSpeed * comp.EnrageSpeedMultiplier);
        _movement.ChangeBaseSpeed(uid, newSpeed, newSpeed, 20f);
    }

    // ── Blood blast ───────────────────────────────────────────────────────────

    private bool TryFindNearbyHostile(Entity<NpcFactionMemberComponent?> self, float range, out EntityUid target)
    {
        target = EntityUid.Invalid;
        float? best = null;
        var ourCoords = Transform(self).Coordinates;

        foreach (var hostile in _faction.GetNearbyHostiles(self, range))
        {
            if (!Exists(hostile) ||
                !TryComp<MobStateComponent>(hostile, out var hs) ||
                hs.CurrentState != MobState.Alive)
                continue;

            if (!ourCoords.TryDistance(EntityManager, Transform(hostile).Coordinates, out var dist))
                continue;

            if (best != null && dist >= best)
                continue;

            best = dist;
            target = hostile;
        }

        return target != EntityUid.Invalid;
    }

    private void DoBloodBlast(EntityUid uid, EntityCoordinates center, BubblegumComponent comp)
    {
        _audio.PlayPvs(comp.BlastSound, uid);

        if (!TryComp<NpcFactionMemberComponent>(uid, out var faction))
            return;

        var spec = new DamageSpecifier();
        spec.DamageDict.Add("Slash", FixedPoint2.New(comp.BlastDamage));

        foreach (var hostile in _faction.GetNearbyHostiles((uid, faction), comp.BlastRadius + 0.5f))
        {
            if (!Exists(hostile)) continue;

            if (!center.TryDistance(EntityManager, Transform(hostile).Coordinates, out var dist) ||
                dist > comp.BlastRadius)
                continue;

            if (!TryComp<DamageableComponent>(hostile, out var damageable))
                continue;

            _damageable.TryChangeDamage((hostile, damageable), spec, origin: uid);
        }
    }

    // ── Clone assault ─────────────────────────────────────────────────────────

    private void DoCloneAssault(EntityUid uid, BubblegumComponent comp, EntityUid target)
    {
        var center = Transform(target).Coordinates;
        for (var i = 0; i < comp.CloneCount; i++)
        {
            var angle = MathF.PI * 2f / comp.CloneCount * i;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * comp.CloneSpawnRadius;
            Spawn(comp.ClonePrototype, center.Offset(offset));
        }
        comp.NextCloneTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CloneCooldown);
    }

    // ── Player / damage helpers ───────────────────────────────────────────────

    private bool TryFindNearbyPlayer(EntityUid uid, float range, out EntityUid result)
    {
        result = EntityUid.Invalid;
        var myPos = Transform(uid).Coordinates;
        var best = float.MaxValue;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (!Exists(candidate)) continue;

            if (!TryComp<MobStateComponent>(candidate, out var ms) ||
                ms.CurrentState != MobState.Alive)
                continue;

            if (!myPos.TryDistance(EntityManager, Transform(candidate).Coordinates, out var dist))
                continue;

            if (dist > range || dist >= best)
                continue;

            best = dist;
            result = candidate;
        }

        return result.Valid;
    }

    private void DamagePlayersNear(EntityUid bossUid, EntityCoordinates center, float damage, float radius)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict.Add("Slash", FixedPoint2.New(damage));

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (!Exists(candidate)) continue;

            if (!TryComp<MobStateComponent>(candidate, out var ms) ||
                ms.CurrentState != MobState.Alive)
                continue;

            if (!TryComp<DamageableComponent>(candidate, out var damageable))
                continue;

            if (!center.TryDistance(EntityManager, Transform(candidate).Coordinates, out var dist) ||
                dist > radius)
                continue;

            _damageable.TryChangeDamage((candidate, damageable), spec, origin: bossUid);
        }
    }

    // ── Tile damage helpers ───────────────────────────────────────────────────

    private void ProcessPendingTileDamage(EntityUid uid)
    {
        if (!_pendingDamage.TryGetValue(uid, out var pending) || pending.Count == 0)
            return;

        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var hit = pending[i];
            if (_timing.CurTime < hit.TriggerTime)
                continue;

            DamageEntitiesOnTile(uid, hit.Tile, hit.Damage);
            pending.RemoveAt(i);
        }
    }

    private bool TrySpawnBloodPuddle(EntityUid uid, BubblegumComponent comp, out EntityUid puddleUid)
    {
        Solution blood = new();
        blood.AddReagent(comp.BloodReagent, FixedPoint2.New(comp.BloodPuddleVolume));

        return _puddle.TrySpillAt(uid, blood, out puddleUid, sound: false);
    }

    private void QueueTileDamage(EntityUid uid, EntityCoordinates tile, float damage, float delay)
    {

        if (!_pendingDamage.TryGetValue(uid, out var pending))
        {
            pending = new List<PendingTileDamage>();
            _pendingDamage[uid] = pending;
        }

        pending.Add(new PendingTileDamage(tile, _timing.CurTime + TimeSpan.FromSeconds(delay), damage));
    }

    private void DamageEntitiesOnTile(EntityUid uid, EntityCoordinates tile, float damage)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict.Add("Slash", FixedPoint2.New(damage));

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (!Exists(candidate)) continue;

            if (!TryComp<MobStateComponent>(candidate, out var ms) ||
                ms.CurrentState != MobState.Alive)
                continue;

            if (!TryComp<DamageableComponent>(candidate, out var damageable))
                continue;

            if (!IsSameTile(tile, SnapToTile(Transform(candidate).Coordinates)))
                continue;

            _damageable.TryChangeDamage((candidate, damageable), spec, origin: uid);
        }
    }

    private static EntityCoordinates SnapToTile(EntityCoordinates coords)
        => new(coords.EntityId, MathF.Round(coords.X), MathF.Round(coords.Y));

    private static bool IsSameTile(EntityCoordinates a, EntityCoordinates b)
        => a.EntityId == b.EntityId
           && MathF.Round(a.X) == MathF.Round(b.X)
           && MathF.Round(a.Y) == MathF.Round(b.Y);
}
