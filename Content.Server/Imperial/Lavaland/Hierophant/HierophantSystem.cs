using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Hierophant;

public sealed class HierophantSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, List<PendingTileDamage>> _pendingDamage = new();
    private readonly Dictionary<EntityUid, SnakeState> _snakeStates = new();
    private readonly Dictionary<EntityUid, List<ChaserState>> _chasers = new();

    private readonly record struct PendingTileDamage(EntityCoordinates Tile, TimeSpan TriggerTime, float Damage);

    private struct SnakeState
    {
        public EntityUid Target;
        public EntityCoordinates CurrentTile;
        public int RemainingTiles;
        public TimeSpan NextStepTime;
    }

    private struct ChaserState
    {
        public EntityUid Target;
        public EntityCoordinates CurrentPos;
        public TimeSpan NextStepTime;
        public TimeSpan EndTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HierophantComponent, NpcFactionMemberComponent, MobStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var hierophant, out var faction, out var mobState, out var damageable))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            ProcessPendingTileDamage(uid);
            ProcessSnake(uid, hierophant);
            ProcessLeap(uid, hierophant);
            ProcessChasers(uid, hierophant);

            if (hierophant.RingAttackActive && _timing.CurTime >= hierophant.NextRingStepTime)
                ExecuteSquareRingStep(uid, hierophant);

            // Teleport to player if they moved too far away
            if (_timing.CurTime >= hierophant.NextTeleportTime &&
                TryFindNearbyPlayer(uid, 40f, out var teleTarget))
            {
                var ourPos = Transform(uid).Coordinates;
                if (ourPos.TryDistance(EntityManager, Transform(teleTarget).Coordinates, out var dist) &&
                    dist >= hierophant.TeleportRange)
                {
                    _audio.PlayPvs(hierophant.TeleportSound, uid);
                    TeleportTowards(uid, teleTarget, hierophant);
                    hierophant.NextTeleportTime = _timing.CurTime + TimeSpan.FromSeconds(hierophant.TeleportCooldown);
                }
            }

            if (_timing.CurTime < hierophant.NextAttackTime)
                continue;

            if (!TryFindNearbyPlayer(uid, hierophant.TargetSearchRange, out var target) &&
                !TryFindNearbyHostile((uid, faction), hierophant.TargetSearchRange, out target))
                continue;

            var healthRatio = GetHealthRatio(uid, damageable);
            var belowHalfHp = healthRatio <= 0.5f;

            // Independent cooldown attacks
            if (_timing.CurTime >= hierophant.NextChaserTime)
                StartChaserAttack(uid, target, hierophant);

            if (_timing.CurTime >= hierophant.NextArenaTime)
                DoArenaAttack(uid, target, hierophant);

            switch (hierophant.AttackIndex)
            {
                case 0:
                    DoLineAttack(uid, hierophant, LineAttackMode.Diagonal);
                    break;
                case 1:
                    DoLineAttack(uid, hierophant, LineAttackMode.Cardinal);
                    break;
                case 2:
                    if (belowHalfHp)
                        DoLineAttack(uid, hierophant, LineAttackMode.Combined);
                    else
                        DoLineAttack(uid, hierophant, LineAttackMode.Diagonal);
                    break;
                case 3:
                    StartSnakeAttack(uid, target, hierophant);
                    break;
                case 4:
                    StartSquareRingAttack(uid, hierophant);
                    break;
                case 5:
                    StartLeapAttack(uid, target, hierophant);
                    break;
            }

            hierophant.AttackIndex = (hierophant.AttackIndex + 1) % 6;
            var scaledCooldown = MathF.Max(hierophant.MinAttackCooldown, hierophant.AttackCooldown * healthRatio);
            hierophant.NextAttackTime = _timing.CurTime + TimeSpan.FromSeconds(scaledCooldown);
        }
    }

    private enum LineAttackMode
    {
        Diagonal,
        Cardinal,
        Combined
    }

    private bool IsBelowHalfHp(EntityUid uid, DamageableComponent damageable)
    {
        return GetHealthRatio(uid, damageable) <= 0.5f;
    }

    private float GetHealthRatio(EntityUid uid, DamageableComponent damageable)
    {
        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return 1f;

        float? deadThreshold = null;
        foreach (var (threshold, state) in thresholds.Thresholds)
        {
            if (state != MobState.Dead)
                continue;

            deadThreshold = threshold.Float();
            break;
        }

        if (deadThreshold == null || deadThreshold.Value <= 0.001f)
            return 1f;

        var totalDamage = _damageable.GetPositiveDamage((uid, damageable)).GetTotal().Float();
        var ratio = 1f - (totalDamage / deadThreshold.Value);
        return MathF.Max(0f, MathF.Min(1f, ratio));
    }

    private bool TryFindNearbyHostile(Entity<NpcFactionMemberComponent?> self, float range, out EntityUid target)
    {
        target = EntityUid.Invalid;

        float? bestDistance = null;
        var ourCoords = Transform(self).Coordinates;

        foreach (var hostile in _faction.GetNearbyHostiles(self, range))
        {
            if (!Exists(hostile) ||
                !TryComp<MobStateComponent>(hostile, out var hostileState) ||
                hostileState.CurrentState != MobState.Alive)
            {
                continue;
            }

            var hostileCoords = Transform(hostile).Coordinates;
            if (!ourCoords.TryDistance(EntityManager, hostileCoords, out var distance))
                continue;

            if (bestDistance != null && distance >= bestDistance)
                continue;

            bestDistance = distance;
            target = hostile;
        }

        return target != EntityUid.Invalid;
    }

    private bool TryFindNearbyPlayer(EntityUid self, float range, out EntityUid target)
    {
        target = EntityUid.Invalid;
        var ourCoords = Transform(self).Coordinates;
        float? bestDistance = null;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (!Exists(candidate) ||
                !TryComp<MobStateComponent>(candidate, out var mobState) ||
                mobState.CurrentState != MobState.Alive)
            {
                continue;
            }

            if (!ourCoords.TryDistance(EntityManager, Transform(candidate).Coordinates, out var distance) || distance > range)
                continue;

            if (bestDistance != null && distance >= bestDistance)
                continue;

            bestDistance = distance;
            target = candidate;
        }

        return target != EntityUid.Invalid;
    }

    private void DoLineAttack(EntityUid uid, HierophantComponent comp, LineAttackMode mode)
    {
        _audio.PlayPvs(comp.LineAttackSound, uid);

        var center = SnapToTile(Transform(uid).Coordinates);
        SpawnDamageTile(uid, center, comp.BlastEffectPrototype, comp.CrossBlastDamage, comp.TileDamageDelay);

        if (mode is LineAttackMode.Cardinal or LineAttackMode.Combined)
        {
            SpawnLineDirections(uid, center, comp, new[]
            {
                new Vector2(1f, 0f),
                new Vector2(-1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, -1f),
            });
        }

        if (mode is LineAttackMode.Diagonal or LineAttackMode.Combined)
        {
            SpawnLineDirections(uid, center, comp, new[]
            {
                new Vector2(1f, 1f),
                new Vector2(-1f, 1f),
                new Vector2(1f, -1f),
                new Vector2(-1f, -1f),
            });
        }
    }

    private void SpawnLineDirections(EntityUid uid, EntityCoordinates center, HierophantComponent comp, IReadOnlyList<Vector2> directions)
    {
        var length = Math.Max(1, comp.LineAttackLength);
        for (var step = 1; step <= length; step++)
        {
            foreach (var direction in directions)
            {
                var tile = SnapToTile(center.Offset(direction * step));
                SpawnDamageTile(uid, tile, comp.BlastEffectPrototype, comp.CrossBlastDamage, comp.TileDamageDelay);
            }
        }
    }

    private void StartSnakeAttack(EntityUid uid, EntityUid target, HierophantComponent comp)
    {
        _audio.PlayPvs(comp.SnakeAttackSound, uid);

        var startTile = SnapToTile(Transform(uid).Coordinates);
        SpawnDamageTile(uid, startTile, comp.SnakeTilePrototype, comp.SnakeTileDamage, comp.TileDamageDelay);

        var max = Math.Max(comp.SnakeMinTiles, comp.SnakeMaxTiles);
        var min = Math.Min(comp.SnakeMinTiles, comp.SnakeMaxTiles);
        var totalTiles = _random.Next(min, max + 1);

        _snakeStates[uid] = new SnakeState
        {
            Target = target,
            CurrentTile = startTile,
            RemainingTiles = Math.Max(0, totalTiles - 1),
            NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SnakeStepDelay)
        };
    }

    private void ProcessSnake(EntityUid uid, HierophantComponent comp)
    {
        if (!_snakeStates.TryGetValue(uid, out var state))
            return;

        if (_timing.CurTime < state.NextStepTime)
            return;

        if (state.RemainingTiles <= 0 || !Exists(state.Target) ||
            !TryComp<MobStateComponent>(state.Target, out var targetMob) || targetMob.CurrentState != MobState.Alive)
        {
            _snakeStates.Remove(uid);
            return;
        }

        var targetTile = SnapToTile(Transform(state.Target).Coordinates);
        var nextTile = StepSnakeTile(state.CurrentTile, targetTile);

        SpawnDamageTile(uid, nextTile, comp.SnakeTilePrototype, comp.SnakeTileDamage, comp.TileDamageDelay);

        state.CurrentTile = nextTile;
        state.RemainingTiles--;
        state.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SnakeStepDelay);
        _snakeStates[uid] = state;
    }

    private EntityCoordinates StepSnakeTile(EntityCoordinates current, EntityCoordinates target)
    {
        var dx = MathF.Round(target.X) - MathF.Round(current.X);
        var dy = MathF.Round(target.Y) - MathF.Round(current.Y);

        var xStep = 0f;
        var yStep = 0f;

        if (Math.Abs(dx) >= Math.Abs(dy) && Math.Abs(dx) > 0.001f)
            xStep = MathF.Sign(dx);
        else if (Math.Abs(dy) > 0.001f)
            yStep = MathF.Sign(dy);

        if (xStep == 0f && yStep == 0f)
            xStep = 1f;

        return SnapToTile(current.Offset(new Vector2(xStep, yStep)));
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

            DamagePlayersOnTile(uid, hit.Tile, hit.Damage);
            pending.RemoveAt(i);
        }
    }

    private void SpawnDamageTile(EntityUid uid, EntityCoordinates tile, string effectProto, float damage, float delay)
    {
        Spawn(effectProto, tile);

        if (!_pendingDamage.TryGetValue(uid, out var pending))
        {
            pending = new List<PendingTileDamage>();
            _pendingDamage[uid] = pending;
        }

        pending.Add(new PendingTileDamage(tile, _timing.CurTime + TimeSpan.FromSeconds(delay), damage));
    }

    private void StartSquareRingAttack(EntityUid uid, HierophantComponent comp)
    {
        _audio.PlayPvs(comp.RingAttackSound, uid);
        comp.RingAttackActive = true;
        comp.CurrentRingStep = 1;
        comp.NextRingStepTime = _timing.CurTime;
    }

    private void ExecuteSquareRingStep(EntityUid uid, HierophantComponent comp)
    {
        if (!comp.RingAttackActive)
            return;

        var ring = comp.CurrentRingStep;
        var halfSize = ring;
        var center = SnapToTile(Transform(uid).Coordinates);
        var damage = comp.SquareRingBaseDamage + (ring - 1) * comp.SquareRingDamageStep;

        for (int x = -halfSize; x <= halfSize; x++)
        {
            for (int y = -halfSize; y <= halfSize; y++)
            {
                if (Math.Abs(x) != halfSize && Math.Abs(y) != halfSize)
                    continue;

                var offset = new Vector2(x, y);
                var blastCoords = SnapToTile(center.Offset(offset));
                SpawnDamageTile(uid, blastCoords, comp.SquareEffectPrototype, damage, comp.TileDamageDelay);
            }
        }

        if (ring >= comp.SquareRingCount)
        {
            comp.RingAttackActive = false;
            return;
        }

        comp.CurrentRingStep++;
        comp.NextRingStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SquareRingStepDelay);
    }

    private void DamagePlayersOnTile(EntityUid uid, EntityCoordinates tile, float damage)
    {
        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict.Add("Blunt", FixedPoint2.New(damage));

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (!Exists(candidate))
                continue;

            if (!TryComp<MobStateComponent>(candidate, out var state) || state.CurrentState != MobState.Alive)
                continue;

            if (!TryComp<DamageableComponent>(candidate, out var damageable))
                continue;

            var entityTile = SnapToTile(Transform(candidate).Coordinates);
            if (!IsSameTile(tile, entityTile))
                continue;

            _damageable.TryChangeDamage((candidate, damageable), damageSpec, origin: uid);
        }
    }

    private bool IsSameTile(EntityCoordinates a, EntityCoordinates b)
    {
        return a.EntityId == b.EntityId &&
               MathF.Round(a.X) == MathF.Round(b.X) &&
               MathF.Round(a.Y) == MathF.Round(b.Y);
    }

    private EntityCoordinates SnapToTile(EntityCoordinates coords)
    {
        return new EntityCoordinates(coords.EntityId, MathF.Round(coords.X), MathF.Round(coords.Y));
    }

    // ── Chaser attack ─────────────────────────────────────────────────────────

    private void StartChaserAttack(EntityUid uid, EntityUid target, HierophantComponent comp)
    {
        _audio.PlayPvs(comp.ChaserAttackSound, uid);

        if (!_chasers.TryGetValue(uid, out var list))
        {
            list = new List<ChaserState>();
            _chasers[uid] = list;
        }

        // Remove expired chasers first
        list.RemoveAll(c => _timing.CurTime >= c.EndTime);

        var count = comp.MaxChasers;
        // Below 50% HP: fire an extra chaser
        if (TryComp<DamageableComponent>(uid, out var dmg) && GetHealthRatio(uid, dmg) <= 0.5f)
            count++;

        for (var i = 0; i < count; i++)
        {
            list.Add(new ChaserState
            {
                Target = target,
                CurrentPos = SnapToTile(Transform(uid).Coordinates),
                NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ChaserStepDelay * (i + 1)),
                EndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ChaserDuration),
            });
        }

        comp.NextChaserTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ChaserCooldown);
    }

    private void ProcessChasers(EntityUid uid, HierophantComponent comp)
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
            var nextPos = StepSnakeTile(chaser.CurrentPos, targetTile);

            SpawnDamageTile(uid, nextPos, comp.BlastEffectPrototype, comp.ChaserDamage, comp.TileDamageDelay);

            chaser.CurrentPos = nextPos;
            chaser.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ChaserStepDelay);
            list[i] = chaser;
        }

        if (list.Count == 0)
            _chasers.Remove(uid);
    }

    // ── Arena trap ────────────────────────────────────────────────────────────

    private void DoArenaAttack(EntityUid uid, EntityUid target, HierophantComponent comp)
    {
        _audio.PlayPvs(comp.ArenaAttackSound, uid);

        var center = SnapToTile(Transform(target).Coordinates);
        var r = comp.ArenaRadius;

        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                if (Math.Abs(x) != r && Math.Abs(y) != r)
                    continue; // Only the perimeter ring

                var tile = SnapToTile(center.Offset(new Vector2(x, y)));
                SpawnDamageTile(uid, tile, comp.SquareEffectPrototype, comp.SquareRingBaseDamage, comp.TileDamageDelay * 1.5f);
            }
        }

        comp.NextArenaTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ArenaCooldown);
    }

    private void TeleportTowards(EntityUid uid, EntityUid target, HierophantComponent comp)
    {
        var ourCoords = Transform(uid).Coordinates;
        var targetCoords = Transform(target).Coordinates;

        if (!ourCoords.TryDistance(EntityManager, targetCoords, out var distance) || distance < 0.1f)
            return;

        // Place boss 2 tiles away from target
        var dir = new Vector2(targetCoords.X - ourCoords.X, targetCoords.Y - ourCoords.Y) / distance;
        var dest = targetCoords.Offset(-dir * 2f);
        _transform.SetCoordinates(uid, dest);
    }

    private void StartLeapAttack(EntityUid uid, EntityUid target, HierophantComponent comp)
    {
        _audio.PlayPvs(comp.LeapAttackSound, uid);

        var ourXform = Transform(uid);
        var ourCoords = ourXform.Coordinates;
        var targetCoords = Transform(target).Coordinates;

        if (!ourCoords.TryDistance(EntityManager, targetCoords, out var distance) || distance < 0.1f)
            return;

        var dir = new Vector2(targetCoords.X - ourCoords.X, targetCoords.Y - ourCoords.Y) / distance;
        var leapDist = MathF.Min(comp.LeapDistance, MathF.Max(0f, distance - 1.5f));
        var destCoords = ourCoords.Offset(dir * leapDist);

        // Spawn tiles at origin
        SpawnLeapSquare(uid, ourCoords, comp);

        // Teleport
        _transform.SetCoordinates(uid, destCoords);

        // Spawn tiles at destination
        SpawnLeapSquare(uid, destCoords, comp);

        // If below 50% HP, schedule repeats
        if (TryComp<DamageableComponent>(uid, out var dmg) && GetHealthRatio(uid, dmg) <= 0.5f && comp.LeapMaxRepeats > 0)
        {
            comp.LeapActive = true;
            comp.LeapTarget = target;
            comp.LeapRemainingRepeats = comp.LeapMaxRepeats;
            comp.NextLeapTime = _timing.CurTime + TimeSpan.FromSeconds(comp.LeapRepeatDelay);
        }
    }

    private void ProcessLeap(EntityUid uid, HierophantComponent comp)
    {
        if (!comp.LeapActive)
            return;

        if (_timing.CurTime < comp.NextLeapTime)
            return;

        if (comp.LeapRemainingRepeats <= 0 ||
            !Exists(comp.LeapTarget) ||
            !TryComp<MobStateComponent>(comp.LeapTarget, out var targetState) ||
            targetState.CurrentState != MobState.Alive)
        {
            comp.LeapActive = false;
            comp.LeapTarget = EntityUid.Invalid;
            return;
        }

        var ourXform = Transform(uid);
        var ourCoords = ourXform.Coordinates;
        var targetCoords = Transform(comp.LeapTarget).Coordinates;

        if (!ourCoords.TryDistance(EntityManager, targetCoords, out var distance) || distance < 0.1f)
        {
            comp.LeapActive = false;
            comp.LeapTarget = EntityUid.Invalid;
            return;
        }

        _audio.PlayPvs(comp.LeapAttackSound, uid);

        var dir = new Vector2(targetCoords.X - ourCoords.X, targetCoords.Y - ourCoords.Y) / distance;
        var leapDist = MathF.Min(comp.LeapDistance, MathF.Max(0f, distance - 1.5f));
        var destCoords = ourCoords.Offset(dir * leapDist);

        SpawnLeapSquare(uid, ourCoords, comp);
        _transform.SetCoordinates(uid, destCoords);
        SpawnLeapSquare(uid, destCoords, comp);

        comp.LeapRemainingRepeats--;
        comp.NextLeapTime = _timing.CurTime + TimeSpan.FromSeconds(comp.LeapRepeatDelay);

        if (comp.LeapRemainingRepeats <= 0)
        {
            comp.LeapActive = false;
            comp.LeapTarget = EntityUid.Invalid;
        }
    }

    private void SpawnLeapSquare(EntityUid uid, EntityCoordinates center, HierophantComponent comp)
    {
        var r = comp.LeapTileRadius;
        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                var tile = SnapToTile(center.Offset(new Vector2(x, y)));
                SpawnDamageTile(uid, tile, comp.SquareEffectPrototype, comp.SquareRingBaseDamage, comp.TileDamageDelay);
            }
        }
    }
}
