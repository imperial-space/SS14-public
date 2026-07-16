using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;
using System.Linq;

namespace Content.Server.Imperial.Lavaland.Zlatozhor;

public sealed class ZlatozhorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // Руды, выпадающие при смерти
    private static readonly string[] DeathOres =
        ["GoldOre1", "SilverOre1", "DiamondOre1", "UraniumOre1"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZlatozhorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZlatozhorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ZlatozhorComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMapInit(Entity<ZlatozhorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.BurrowTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.BurrowDelay);
    }

    // ── Нырок под землю ─────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZlatozhorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState, out _))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (HasNearbyPlayer(uid, comp.DangerVisionRange))
                TriggerPanicBurrow(comp);

            if (_timing.CurTime < comp.BurrowTime)
                continue;

            TryBurrow(uid, comp);
        }
    }

    private void TryBurrow(EntityUid uid, ZlatozhorComponent comp)
    {
        var xform = Transform(uid);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            QueueDel(uid);
            return;
        }

        var tiles = _mapSystem.GetAllTiles(gridUid, grid).ToList();
        if (tiles.Count == 0)
        {
            QueueDel(uid);
            return;
        }

        var tile = _random.Pick(tiles);
        var newCoords = _mapSystem.GridTileToLocal(gridUid, grid, tile.GridIndices)
            .Offset(new Vector2(0.5f, 0.5f));

        _audio.PlayPvs("/Audio/Effects/teleport_departure.ogg", uid);
        _transform.SetCoordinates(uid, newCoords);
        _audio.PlayPvs("/Audio/Effects/teleport_arrival.ogg", uid);

        // Сбросить таймер
        comp.BurrowTime = _timing.CurTime + TimeSpan.FromSeconds(comp.BurrowDelay);
    }

    // ── Смерть — выдача руды ─────────────────────────────────────────────────

    private void OnMobStateChanged(Entity<ZlatozhorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var ore in DeathOres)
        {
            for (var i = 0; i < ent.Comp.OreDropAmount; i++)
            {
                var offset = new Vector2(
                    (_random.NextFloat() - 0.5f) * 1.5f,
                    (_random.NextFloat() - 0.5f) * 1.5f);
                Spawn(ore, coords.Offset(offset));
            }
        }
    }

    private void OnDamageChanged(Entity<ZlatozhorComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (!TryComp<MobStateComponent>(ent, out var mobState) || mobState.CurrentState != MobState.Alive)
            return;

        TriggerPanicBurrow(ent.Comp);
    }

    // ── Отталкивание при ударе ───────────────────────────────────────────────

    private void OnMeleeHit(MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            if (!TryComp<ZlatozhorComponent>(hit, out var comp))
                continue;

            if (!TryComp<MobStateComponent>(hit, out var state) || state.CurrentState != MobState.Alive)
                continue;

            if (!TryComp<PhysicsComponent>(args.User, out _))
                continue;

            var zlatozhorPos = _transform.GetWorldPosition(hit);
            var attackerPos = _transform.GetWorldPosition(args.User);
            var raw = attackerPos - zlatozhorPos;
            var dir = raw.LengthSquared() > 0.0001f ? Vector2.Normalize(raw) : new Vector2(1f, 0f);

            _physics.ApplyLinearImpulse(args.User, dir * comp.KnockbackForce);
            TriggerPanicBurrow(comp);
        }
    }

    private bool HasNearbyPlayer(EntityUid uid, float range)
    {
        var selfPos = _transform.GetWorldPosition(uid);
        var rangeSquared = range * range;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } player || Deleted(player))
                continue;

            var playerPos = _transform.GetWorldPosition(player);
            if ((playerPos - selfPos).LengthSquared() <= rangeSquared)
                return true;
        }

        return false;
    }

    private void TriggerPanicBurrow(ZlatozhorComponent comp)
    {
        var panicTime = _timing.CurTime + TimeSpan.FromSeconds(comp.PanicBurrowDelay);
        if (panicTime < comp.BurrowTime)
            comp.BurrowTime = panicTime;
    }
}
