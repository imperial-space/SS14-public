using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland.HierophantClub;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.HierophantClub;

public sealed class HierophantClubSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    // Per-staff state
    private readonly Dictionary<EntityUid, List<PendingTileDamage>> _pendingDamage = new();
    private readonly Dictionary<EntityUid, List<ChaserState>> _chasers = new();
    private readonly Dictionary<EntityUid, RingState> _ringStates = new();

    private record struct PendingTileDamage(
        EntityCoordinates Tile,
        TimeSpan TriggerTime,
        float Damage,
        EntityUid Owner);

    private struct ChaserState
    {
        public EntityUid Target;
        public EntityUid Owner;
        public EntityCoordinates CurrentPos;
        public TimeSpan NextStepTime;
        public TimeSpan EndTime;
    }

    private struct RingState
    {
        public EntityCoordinates Center;
        public EntityUid Owner;
        public int CurrentStep;
        public int MaxSteps;
        public float BaseDamage;
        public float DamageStep;
        public float TileDamageDelay;
        public float StepDelay;
        public string SquareEffect;
        public TimeSpan NextStepTime;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HierophantClubComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<HierophantClubComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HierophantClubComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Charge regeneration
            if (comp.Charges < comp.MaxCharges && _timing.CurTime >= comp.NextChargeTime)
            {
                comp.Charges++;
                if (comp.Charges < comp.MaxCharges)
                    comp.NextChargeTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ChargeRegenTime);
                UpdateAppearance((uid, comp));
            }

            ProcessPendingTileDamage(uid);
            ProcessChasers(uid, comp);
            ProcessRing(uid);
        }
    }

    private void OnAfterInteract(Entity<HierophantClubComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Charges <= 0)
        {
            _audio.PlayPvs(ent.Comp.NoChargesSound, ent);
            _popup.PopupEntity(Loc.GetString("hierophant-club-no-charges"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        // Entity click → chaser attack
        if (args.Target is { } target && target.IsValid() && target != args.User)
        {
            ConsumeCharge(ent);
            StartChaserAttack(ent, args.User, target);
            return;
        }

        // Tile click
        var userCoords = Transform(args.User).Coordinates;
        var clickCoords = args.ClickLocation;

        if (!userCoords.TryDistance(EntityManager, clickCoords, out var distance))
            return;

        ConsumeCharge(ent);

        if (distance <= ent.Comp.NearbyTileRange)
            StartSquareRingAttack(ent, args.User, clickCoords);
        else
            DoLineAttack(ent, args.User, clickCoords);
    }

    private void OnShutdown(Entity<HierophantClubComponent> ent, ref ComponentShutdown args)
    {
        _pendingDamage.Remove(ent);
        _chasers.Remove(ent);
        _ringStates.Remove(ent);
    }

    // ── Charges ───────────────────────────────────────────────────────────────

    private void ConsumeCharge(Entity<HierophantClubComponent> ent)
    {
        var wasMax = ent.Comp.Charges == ent.Comp.MaxCharges;
        ent.Comp.Charges = Math.Max(0, ent.Comp.Charges - 1);

        // Start regen timer only when leaving max-charges state
        if (wasMax)
            ent.Comp.NextChargeTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ChargeRegenTime);

        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<HierophantClubComponent> ent)
    {
        _appearance.SetData(ent, HierophantClubVisuals.HasCharges, ent.Comp.Charges > 0);
    }

    // ── Cross / Line attack (Удар крестом) ────────────────────────────────────

    private void DoLineAttack(Entity<HierophantClubComponent> ent, EntityUid owner, EntityCoordinates center)
    {
        _audio.PlayPvs(ent.Comp.LineAttackSound, ent);

        var snapped = SnapToTile(center);
        SpawnDamageTile(ent, snapped, ent.Comp.BlastEffectPrototype, ent.Comp.CrossBlastDamage, ent.Comp.TileDamageDelay, owner);

        var directions = new[]
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(0f, 1f), new Vector2(0f, -1f),
        };

        for (var step = 1; step <= ent.Comp.LineAttackLength; step++)
        {
            foreach (var dir in directions)
            {
                var tile = SnapToTile(snapped.Offset(dir * step));
                SpawnDamageTile(ent, tile, ent.Comp.BlastEffectPrototype, ent.Comp.CrossBlastDamage, ent.Comp.TileDamageDelay, owner);
            }
        }
    }

    // ── Square ring attack (Заряд по площади) ─────────────────────────────────

    private void StartSquareRingAttack(Entity<HierophantClubComponent> ent, EntityUid owner, EntityCoordinates center)
    {
        _audio.PlayPvs(ent.Comp.RingAttackSound, ent);

        _ringStates[ent] = new RingState
        {
            Center = SnapToTile(center),
            Owner = owner,
            CurrentStep = 1,
            MaxSteps = ent.Comp.SquareRingCount,
            BaseDamage = ent.Comp.SquareRingBaseDamage,
            DamageStep = ent.Comp.SquareRingDamageStep,
            TileDamageDelay = ent.Comp.TileDamageDelay,
            StepDelay = ent.Comp.SquareRingStepDelay,
            SquareEffect = ent.Comp.SquareEffectPrototype,
            NextStepTime = _timing.CurTime,
        };
    }

    private void ProcessRing(EntityUid uid)
    {
        if (!_ringStates.TryGetValue(uid, out var state))
            return;

        if (_timing.CurTime < state.NextStepTime)
            return;

        var ring = state.CurrentStep;
        var damage = state.BaseDamage + (ring - 1) * state.DamageStep;

        for (var x = -ring; x <= ring; x++)
        {
            for (var y = -ring; y <= ring; y++)
            {
                if (Math.Abs(x) != ring && Math.Abs(y) != ring)
                    continue;

                var tile = SnapToTile(state.Center.Offset(new Vector2(x, y)));
                SpawnDamageTile(uid, tile, state.SquareEffect, damage, state.TileDamageDelay, state.Owner);
            }
        }

        if (ring >= state.MaxSteps)
        {
            _ringStates.Remove(uid);
            return;
        }

        state.CurrentStep++;
        state.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(state.StepDelay);
        _ringStates[uid] = state;
    }

    // ── Chaser attack (Гончие) ────────────────────────────────────────────────

    private void StartChaserAttack(Entity<HierophantClubComponent> ent, EntityUid owner, EntityUid target)
    {
        _audio.PlayPvs(ent.Comp.ChaserAttackSound, ent);

        if (!_chasers.TryGetValue(ent, out var list))
        {
            list = new List<ChaserState>();
            _chasers[ent] = list;
        }

        list.RemoveAll(c => _timing.CurTime >= c.EndTime);

        for (var i = 0; i < ent.Comp.MaxChasers; i++)
        {
            list.Add(new ChaserState
            {
                Target = target,
                Owner = owner,
                CurrentPos = SnapToTile(Transform(owner).Coordinates),
                NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ChaserStepDelay * (i + 1)),
                EndTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ChaserDuration),
            });
        }
    }

    private void ProcessChasers(EntityUid uid, HierophantClubComponent comp)
    {
        if (!_chasers.TryGetValue(uid, out var list) || list.Count == 0)
            return;

        for (var i = list.Count - 1; i >= 0; i--)
        {
            var chaser = list[i];

            if (_timing.CurTime >= chaser.EndTime)
            {
                list.RemoveAt(i);
                continue;
            }

            if (_timing.CurTime < chaser.NextStepTime)
                continue;

            if (!Exists(chaser.Target) ||
                !TryComp<MobStateComponent>(chaser.Target, out var ts) ||
                ts.CurrentState != MobState.Alive)
            {
                list.RemoveAt(i);
                continue;
            }

            var targetTile = SnapToTile(Transform(chaser.Target).Coordinates);
            var nextPos = StepTowards(chaser.CurrentPos, targetTile);

            SpawnDamageTile(uid, nextPos, comp.BlastEffectPrototype, comp.ChaserDamage, comp.TileDamageDelay, chaser.Owner);

            chaser.CurrentPos = nextPos;
            chaser.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ChaserStepDelay);
            list[i] = chaser;
        }

        if (list.Count == 0)
            _chasers.Remove(uid);
    }

    // ── Tile damage helpers ────────────────────────────────────────────────────

    private void SpawnDamageTile(EntityUid uid, EntityCoordinates tile, string effectProto, float damage, float delay, EntityUid owner)
    {
        Spawn(effectProto, tile);

        if (!_pendingDamage.TryGetValue(uid, out var pending))
        {
            pending = new List<PendingTileDamage>();
            _pendingDamage[uid] = pending;
        }

        pending.Add(new PendingTileDamage(tile, _timing.CurTime + TimeSpan.FromSeconds(delay), damage, owner));
    }

    private void ProcessPendingTileDamage(EntityUid uid)
    {
        if (!_pendingDamage.TryGetValue(uid, out var pending) || pending.Count == 0)
            return;

        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var hit = pending[i];
            if (_timing.CurTime < hit.TriggerTime)
                continue;

            DamageEntitiesOnTile(hit.Tile, hit.Damage, hit.Owner);
            pending.RemoveAt(i);
        }
    }

    private void DamageEntitiesOnTile(EntityCoordinates tile, float damage, EntityUid owner)
    {
        var mapCoords = _xform.ToMapCoordinates(tile);
        if (mapCoords.MapId == MapId.Nullspace)
            return;

        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(tile, 0.45f, entities, flags: LookupFlags.Uncontained);

        var spec = new DamageSpecifier();
        spec.DamageDict["Blunt"] = FixedPoint2.New(damage);

        foreach (var entity in entities)
        {
            if (entity == owner)
                continue;
            if (!TryComp<DamageableComponent>(entity, out var damageable))
                continue;
            if (!TryComp<MobStateComponent>(entity, out var mobState) || mobState.CurrentState != MobState.Alive)
                continue;

            _damageable.TryChangeDamage((entity, damageable), spec, origin: owner);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EntityCoordinates SnapToTile(EntityCoordinates coords)
        => new(coords.EntityId, MathF.Round(coords.X), MathF.Round(coords.Y));

    private static EntityCoordinates StepTowards(EntityCoordinates current, EntityCoordinates target)
    {
        var dx = MathF.Round(target.X) - MathF.Round(current.X);
        var dy = MathF.Round(target.Y) - MathF.Round(current.Y);

        var xStep = 0f;
        var yStep = 0f;

        if (MathF.Abs(dx) >= MathF.Abs(dy) && MathF.Abs(dx) > 0.001f)
            xStep = MathF.Sign(dx);
        else if (MathF.Abs(dy) > 0.001f)
            yStep = MathF.Sign(dy);
        else
            xStep = 1f;

        return SnapToTile(current.Offset(new System.Numerics.Vector2(xStep, yStep)));
    }
}
