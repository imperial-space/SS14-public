using System.Linq;
using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.Minigames;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Imperial.Minigames.Events;
using Content.Shared.Interaction;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MinigamesSystem _minigames = default!;

    private void InitializeAttacks()
    {
        SubscribeLocalEvent<DamageBossOnDestructionComponent, DestructionEventArgs>(OnDamageBossOnDestruction);
        SubscribeLocalEvent<CursedMarkComponent, ComponentInit>(OnMarkInit);
        SubscribeLocalEvent<TimedBossHealComponent, ComponentInit>(OnHealInit);
        SubscribeLocalEvent<TimedBossHealComponent, BossHealDoAfterEvent>(OnBossHealDoAfter);
        SubscribeLocalEvent<PhysAwakeOnSpawnComponent, ComponentInit>(OnAwakeSpawn);
        SubscribeLocalEvent<DamageOnContactComponent, StartCollideEvent>(OnDamageOnContactCollide);
        SubscribeLocalEvent<ChargingRuneExplosionComponent, ComponentInit>(OnChargingStartup);
        SubscribeLocalEvent<ChargingRuneExplosionComponent, BossRunesChargingDoAfterEvent>(OnChargingDoAfter);
        SubscribeLocalEvent<BossBullethellSourceComponent, MapInitEvent>(OnBHellInit);
        SubscribeLocalEvent<BossBullethellSourceComponent, ComponentShutdown>(OnBHellShutdown);
        SubscribeLocalEvent<TrapPlayersOnMapComponent, MapInitEvent>(OnTrapInit);
        SubscribeLocalEvent<TrapPlayersOnMapComponent, AfterInteractUsingEvent>(OnTrapUse);
        SubscribeLocalEvent<TrapPlayersOnMapComponent, EntityTerminatingEvent>(OnTrapTerminating);
        SubscribeLocalEvent<BossTrapMinigameActorComponent, WinInMinigamEvent>(OnMinigameComplete);
        SubscribeLocalEvent<SpikedGridComponent, ComponentInit>(OnSpikedInit);
    }

    private void OnDamageBossOnDestruction(EntityUid uid, DamageBossOnDestructionComponent component, DestructionEventArgs args)
    {
        if (!TryComp<BossAttackComponent>(uid, out var bossAttack))
            return;

        DamageBoss(bossAttack.Boss, component.DamageAmount);
    }

    private void OnMarkInit(EntityUid uid, CursedMarkComponent component, ComponentInit args)
    {
        component.ExplodeTime = _timing.CurTime + TimeSpan.FromSeconds(component.Delay);

        var ent = Spawn("CursedMarkPseudoEntity");
        _transform.SetParent(ent, uid);
        _transform.SetLocalPositionNoLerp(ent, Vector2.Zero);
        component.NetEntity = GetNetEntity(ent);
        Dirty(uid, component);
    }

    private void OnHealInit(EntityUid uid, TimedBossHealComponent component, ComponentInit args)
    {
        var doAfter = new DoAfterArgs(EntityManager, uid, component.Duration, new BossHealDoAfterEvent(), uid)
        {
            BreakOnDamage = false,
            BreakOnMove = false,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBossHealDoAfter(EntityUid uid, TimedBossHealComponent component, BossHealDoAfterEvent args)
    {
        if (!TryComp<BossAttackComponent>(uid, out var attack))
            return;

        if (!TryComp<BossComponent>(attack.Boss, out var bossComp))
            return;

        DamageBoss(attack.Boss, -component.HealAmount);

        RemComp<TimedBossHealComponent>(uid);

        if (EntityQuery<TimedBossHealComponent>().Any())
            return;

        _appearance.SetData(attack.Boss, AdditionalBossVisuals.State, "healing");
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(0.9f), () =>
        {
            _appearance.SetData(attack.Boss, AdditionalBossVisuals.State, "none");
        });
    }

    private void OnAwakeSpawn(EntityUid uid, PhysAwakeOnSpawnComponent component, ComponentInit args)
        => _physics.WakeBody(uid);

    private void OnDamageOnContactCollide(EntityUid uid, DamageOnContactComponent component, ref StartCollideEvent args)
        => _damage.TryChangeDamage(args.OtherEntity, component.Damage);

    private void OnChargingStartup(EntityUid uid, ChargingRuneExplosionComponent component, ComponentInit args)
    {
        var doAfter = new DoAfterArgs(EntityManager, uid, component.Time, new BossRunesChargingDoAfterEvent(), uid)
        {
            BreakOnDamage = false,
            BreakOnMove = false,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter, out component.DoAfter);
    }

    private void OnChargingDoAfter(EntityUid uid, ChargingRuneExplosionComponent component, BossRunesChargingDoAfterEvent args)
    {
        _explosion.QueueExplosion(_transform.ToMapCoordinates(Transform(uid).Coordinates), ExplosionSystem.DefaultExplosionPrototypeId, 1200f, 0.1f, 15, null, 0, 0, false);
        RemComp<ChargingRuneExplosionComponent>(uid);

        var runes = EntityManager.AllEntities<PurifyableExplosionRuneComponent>().Where(x => TryComp<BossAttackComponent>(x, out var attack) && attack.Boss == uid);
        foreach (var item in runes)
            QueueDel(item);
    }

    private void OnBHellInit(EntityUid uid, BossBullethellSourceComponent component, MapInitEvent args)
        => component.Weapon = Spawn(component.WeaponProto);

    private void OnBHellShutdown(EntityUid uid, BossBullethellSourceComponent component, ComponentShutdown args)
        => QueueDel(component.Weapon);

    private void OnTrapInit(EntityUid uid, TrapPlayersOnMapComponent component, MapInitEvent args)
    {
        var players = new List<EntityUid>();
        var query = AllEntityQuery<FightingBossComponent>();
        while (query.MoveNext(out var target, out var comp))
        {
            if (Transform(target).GridUid != null && Transform(target).GridUid == Transform(uid).GridUid)
                players.Add(target);
        }

        if (component.TrapCount >= players.Count)
        {
            QueueDel(uid);
            return;
        }

        if (!_mapLoader.TryLoadMap(component.MapPath, out var map, out var grids, new DeserializationOptions() { InitializeMaps = true }))
            return;

        var mapId = map.Value.Comp.MapId;
        var ents = grids.Select(x => Transform(x.Owner).ChildEnumerator);
        var spawns = new List<EntityCoordinates>();

        foreach (var item in ents)
        {
            while (item.MoveNext(out var target))
            {
                if (_tag.HasTag(target, component.SpawnTag))
                    spawns.Add(Transform(target).Coordinates);
            }
        }

        _random.Shuffle(players);

        for (var i = 0; i < component.TrapCount && i < players.Count; i++)
        {
            _transform.SetCoordinates(players[i], _random.Pick(spawns));
            component.Trapped.Add(players[i]);
            EnsureComp<BossTrappedComponent>(players[i]);
        }
    }

    private void OnTrapUse(EntityUid uid, TrapPlayersOnMapComponent component, AfterInteractUsingEvent args)
    {
        var user = args.User;
        if (component.ReleaseUser.HasValue)
        {
            _popup.PopupEntity("Кто-то уже занимается этим!", uid, user);
            return;
        }

        if (HasComp<BossTrapMinigameActorComponent>(user))
            return;

        if (_minigames.TryStartMinigame(user, component.Minigame))
        {
            var comp = EnsureComp<BossTrapMinigameActorComponent>(user);
            comp.Trap = uid;
        }
    }

    private void OnTrapTerminating(EntityUid uid, TrapPlayersOnMapComponent component, EntityTerminatingEvent args)
        => ReleaseTrapped(uid);

    private void OnMinigameComplete(EntityUid uid, BossTrapMinigameActorComponent component, WinInMinigamEvent args)
        => ReleaseTrapped(component.Trap);

    private void OnSpikedInit(EntityUid uid, SpikedGridComponent component, ComponentInit args)
    {
        if (!component.RandomDirection)
            return;

        component.Direction = (SpikedGridDirection)_random.Next(4);
        component.TargetIndex = _random.Pick(component.TargetIndexesPossible);

        // Это если вдруг будет грид с разными значениями горизонтали и вертикали, а не квадрат как щас
        //switch (component.Direction)
        //{
        //    case SpikedGridDirection.Left:
        //    case SpikedGridDirection.Right:
        //        component.TargetIndex = _random.Prob(0.5f) ? 17 : 10;
        //        break;
        //    case SpikedGridDirection.Up:
        //    case SpikedGridDirection.Down:
        //        component.TargetIndex = _random.Prob(0.5f) ? 17 : 10;
        //        break;
        //}
    }

    private void ReleaseTrapped(EntityUid uid, TrapPlayersOnMapComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Map is not { Valid: true })
            return;

        var coords = Transform(uid).Coordinates;
        foreach (var item in component.Trapped)
        {
            _transform.SetCoordinates(item, coords);
            RemComp<BossTrappedComponent>(item);
        }

        _map.DeleteMap(Comp<MapComponent>(component.Map).MapId);
        QueueDel(uid);
        SpawnAtPosition(component.SpawnOnDespawn, coords);
    }

    private void UpdateSpiked()
    {
        var query = EntityQueryEnumerator<SpikedGridComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var spikedComp, out var grid))
        {
            if (spikedComp.NextSpawn > _timing.CurTime)
                continue;

            if (spikedComp.NextIndex > spikedComp.TargetIndex)
            {
                RemComp<SpikedGridComponent>(uid);
                continue;
            }

            var tiles = _map.GetAllTiles(uid, grid);

            // Используем Func<T, bool> для фильтрации плиток, чтобы избежать дублирования кода.
            Func<TileRef, bool> filter;
            int idx;

            switch (spikedComp.Direction)
            {
                case SpikedGridDirection.Right:
                    var xIndicesRight = tiles.Select(t => t.GridIndices.X);
                    idx = xIndicesRight.Min() + spikedComp.NextIndex;
                    filter = t => t.GridIndices.X == idx;
                    break;

                case SpikedGridDirection.Left:
                    var xIndicesLeft = tiles.Select(t => t.GridIndices.X);
                    idx = xIndicesLeft.Max() - spikedComp.NextIndex;
                    filter = t => t.GridIndices.X == idx;
                    break;

                case SpikedGridDirection.Up:
                    var yIndicesUp = tiles.Select(t => t.GridIndices.Y);
                    idx = yIndicesUp.Min() + spikedComp.NextIndex;
                    filter = t => t.GridIndices.Y == idx;
                    break;

                case SpikedGridDirection.Down:
                    var yIndicesDown = tiles.Select(t => t.GridIndices.Y);
                    idx = yIndicesDown.Max() - spikedComp.NextIndex;
                    filter = t => t.GridIndices.Y == idx;
                    break;

                default:
                    continue;
            }

            foreach (var tile in tiles.Where(filter))
            {
                SpawnAtPosition(spikedComp.SpikeProto, new(uid, tile.GridIndices));
            }

            spikedComp.NextIndex++;
            spikedComp.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(spikedComp.SpawnInterval);
        }
    }

    private void UpdateMark()
    {
        var query = EntityQueryEnumerator<CursedMarkComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ExplodeTime > _timing.CurTime)
                continue;

            if (_lookup.GetEntitiesInRange<FightingBossComponent>(Transform(uid).Coordinates, 3f).Count > 1)
                _explosion.QueueExplosion(_transform.ToMapCoordinates(Transform(uid).Coordinates), ExplosionSystem.DefaultExplosionPrototypeId, 85, 10, 7, null, 0, 0, false);

            QueueDel(GetEntity(comp.NetEntity));
            RemComp<CursedMarkComponent>(uid);
        }
    }

    private void UpdateSpikeMarker()
    {
        var query = EntityQueryEnumerator<SpikeAttackMarkerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextSpawn > _timing.CurTime)
                continue;

            if (comp.Idx > comp.TargetIdx)
            {
                RemComp<SpikeAttackMarkerComponent>(uid);
                continue;
            }

            var pos = Transform(uid).Coordinates;
            foreach (var item in comp.Positions[comp.Idx])
            {
                SpawnAtPosition(comp.Proto, new EntityCoordinates(pos.EntityId, pos.Position + item));
            }

            comp.Idx++;
            comp.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(comp.SpawnInterval);
        }
    }

    private void UpdateRunes()
    {
        var query = EntityQueryEnumerator<ChargingRuneExplosionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextCheck > _timing.CurTime)
                continue;

            comp.NextCheck = _timing.CurTime + TimeSpan.FromSeconds(1f);

            var runes = EntityManager.AllEntities<PurifyableExplosionRuneComponent>().Where(x => TryComp<BossAttackComponent>(x, out var attack) && attack.Boss == uid);
            var activeRunes = runes.Where(x => _physics.GetContactingEntities(x).Where(y => HasComp<HumanoidAppearanceComponent>(y)).ToList().Any());

            if (activeRunes.Count() >= runes.Count())
            {
                foreach (var item in runes)
                    QueueDel(item);

                RemComp<ChargingRuneExplosionComponent>(uid);
                _doAfter.Cancel(comp.DoAfter);
                continue;
            }
        }
    }

    private void UpdateBHell()
    {
        var query = EntityQueryEnumerator<BossBullethellSourceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextShot > _timing.CurTime)
                continue;

            if (comp.CurShot == 1)
            {
                comp.TargetPos = new Vector2(0, 1);
                if (comp.RandomRotation)
                    comp.TargetPos = _random.NextAngle().ToVec();
                if (comp.RandomizeNegative)
                    comp.Negative = _random.Prob(0.5f);
            }
            else
            {
                var transform = Matrix3x2.CreateRotation((float)Angle.FromDegrees(comp.Negative ? -comp.DegreesPerShot : comp.DegreesPerShot).Theta);
                comp.TargetPos = Vector2.Transform(comp.TargetPos, transform);
            }

            var coords = Transform(uid).Coordinates;
            var targetCoords = new EntityCoordinates(coords.EntityId, coords.Position + comp.TargetPos);
            _gun.AttemptShoot(uid, comp.Weapon, Comp<GunComponent>(comp.Weapon), targetCoords);
            _audio.PlayPvs(Comp<GunComponent>(comp.Weapon).SoundGunshot, uid);
            comp.CurShot++;
            comp.NextShot = _timing.CurTime + TimeSpan.FromSeconds(comp.Delay);

            if (comp.CurShot > comp.Shots)
                RemComp<BossBullethellSourceComponent>(uid);
        }
    }
}
