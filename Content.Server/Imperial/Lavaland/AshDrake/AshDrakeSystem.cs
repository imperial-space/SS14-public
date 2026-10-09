using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Server.Tiles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Lavaland.AshDrake;

/// <summary>
/// Перенос megafauna/dragon из SS13: OpenFire, fire_breath (cone/mass_fire), meteors,
/// lava_swoop (lava_pools, lava_arena) и arena_escape_enrage.
/// </summary>
public sealed class AshDrakeSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private static readonly Color EnrageColor = Color.FromHex("#FFFF00");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AshDrakeComponent, MegafaunaOpenFireEvent>(OnOpenFire);
        SubscribeLocalEvent<AshDrakeComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<AshDrakeComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnAttemptMelee(Entity<AshDrakeComponent> ent, ref AttemptMeleeEvent args)
    {
        if (ent.Comp.Swooping)
            args.Cancelled = true;
    }

    private void OnMobStateChanged(Entity<AshDrakeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        ent.Comp.Swooping = false;
        _physics.SetCanCollide(ent, true);
        _appearance.SetData(ent, MegafaunaVisuals.State, "dragon_dead");
        _appearance.SetData(ent, MegafaunaVisuals.Color, Color.White);
    }

    private bool Enraged(EntityUid uid)
    {
        return _ai.GetHealth(uid) < _ai.GetMaxHealth(uid) * 0.5f;
    }

    private void OnOpenFire(Entity<AshDrakeComponent> ent, ref MegafaunaOpenFireEvent args)
    {
        if (ent.Comp.Swooping)
            return;

        var target = args.Target;
        var anger = _ai.Anger(ent, 60f, 20f);

        if (_ai.Prob(15 + anger))
        {
            if (Enraged(ent))
            {
                // Lava Arena
                TryLavaSwoop(ent, target, null);
                return;
            }

            // Lava Pools
            if (TryLavaSwoop(ent, target, () =>
                {
                    _ai.ResetCooldown(ent);
                    TryFireCone(ent, target);
                    _ai.ResetCooldown(ent);
                    TryMeteors(ent, target);
                }))
            {
                return;
            }
        }
        else if (_ai.Prob(10 + anger) && Enraged(ent))
        {
            TryMassFire(ent, target);
            return;
        }

        if (TryFireCone(ent, target) && _ai.Prob(50))
        {
            _ai.ResetCooldown(ent);
            TryMeteors(ent, target);
        }
    }

    #region Fire breath

    private bool TryFireCone(Entity<AshDrakeComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        _ai.PlaySound(ent.Comp.FireSound, Transform(ent).Coordinates, 5f);
        foreach (var offset in ent.Comp.FireConeAngles)
            FireLine(ent, target, offset);

        _ai.EndAbility(ent, ent.Comp.FireConeCooldown);
        return true;
    }

    private bool TryMassFire(Entity<AshDrakeComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        MassFire(ent, target);
        _ai.EndAbility(ent, ent.Comp.MassFireCooldown);
        return true;
    }

    /// <summary>mass_fire: три круга по 12 линий с паузой 2.5 с.</summary>
    private void MassFire(Entity<AshDrakeComponent> ent, EntityUid target)
    {
        for (var i = 1; i <= ent.Comp.MassFireSpins; i++)
        {
            var spin = i;
            _ai.Schedule(ent, (spin - 1) * ent.Comp.MassFireBreathDelay, () =>
            {
                _ai.PlaySound(ent.Comp.FireSound, Transform(ent).Coordinates, 5f);
                var increment = 360f / ent.Comp.MassFireSectors;
                var additional = spin * increment / 2f;
                for (var s = 1; s <= ent.Comp.MassFireSectors; s++)
                    FireLine(ent, target, increment * s + additional);
            });
        }
    }

    /// <summary>fire_line: линия огня длиной 15 клеток к цели с отклонением offset (по часовой).</summary>
    private void FireLine(Entity<AshDrakeComponent> ent, EntityUid target, float offset)
    {
        if (TerminatingOrDeleted(target) ||
            !_ai.TryGetTile(ent, out var grid, out var origin) ||
            !_ai.TryGetTile(target, out _, out var targetTile))
            return;

        var angle = MegafaunaAiSystem.AngleTo(origin, targetTile) - offset;
        var end = MegafaunaAiSystem.TileAtAngle(origin, angle, ent.Comp.FireRange);
        var turfs = MegafaunaAiSystem.Line(origin, end).Where(t => t != origin).ToList();
        var hit = new HashSet<EntityUid> { ent };
        ProgressiveFire(ent, grid, turfs, 0, hit);
    }

    private void ProgressiveFire(Entity<AshDrakeComponent> ent, Entity<MapGridComponent> grid, List<Vector2i> turfs, int index, HashSet<EntityUid> hit)
    {
        if (index >= turfs.Count || index == 0 && !_ai.IsAliveBoss(ent))
            return;

        var tile = turfs[index];
        if (_ai.IsBlocked(grid, tile))
            return;

        BurnTurf(ent, grid, tile, hit);
        _ai.Schedule(null, ent.Comp.FireDelay, () => ProgressiveFire(ent, grid, turfs, index + 1, hit));
    }

    /// <summary>burn_turf + on_burn_mob.</summary>
    private void BurnTurf(Entity<AshDrakeComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, HashSet<EntityUid> hit)
    {
        _ai.SpawnAt(ent.Comp.FireEffect, grid, tile);
        foreach (var mob in _ai.MobsOnTile(grid, tile))
        {
            if (!hit.Add(mob))
                continue;

            _popup.PopupEntity(Loc.GetString("ash-drake-fire-breath-hit", ("drake", ent.Owner)), mob, mob, PopupType.LargeCaution);
            _ai.Damage(mob, "Heat", ent.Comp.FireDamage, ent);
            _ai.Ignite(mob);
        }
    }

    #endregion

    #region Meteors

    private bool TryMeteors(Entity<AshDrakeComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        if (_ai.TryGetTile(target, out var grid, out var center))
        {
            _popup.PopupEntity(Loc.GetString("ash-drake-meteors"), target, PopupType.LargeCaution);
            foreach (var tile in MegafaunaAiSystem.RangeTiles(center, ent.Comp.MeteorsRange))
            {
                if (_ai.Prob(ent.Comp.MeteorsChance))
                    MeteorTarget(ent, grid, tile);
            }
        }

        _ai.EndAbility(ent, ent.Comp.MeteorsCooldown);
        return true;
    }

    /// <summary>temp_visual/target: прицел, огненный шар падает 0.9 с, затем взрыв и огонь.</summary>
    private void MeteorTarget(Entity<AshDrakeComponent> ent, Entity<MapGridComponent> grid, Vector2i tile)
    {
        var coords = _ai.TileCenter(grid, tile);
        _ai.PlaySound(ent.Comp.WarnSound, coords);
        _ai.SpawnAt(ent.Comp.MeteorTarget, grid, tile);
        // pixel_z = 270 -> 0 за duration: шар падает сверху на клетку.
        var fireball = Spawn(ent.Comp.MeteorFireball, coords.Offset(new Vector2(0f, 8.4f)));
        FallStep(fireball, 9, 8.4f / 9f);

        _ai.Schedule(null, ent.Comp.MeteorFallTime, () =>
        {
            _ai.Drill(grid, tile);
            _ai.PlaySound(ent.Comp.ExplosionSound, coords);
            _ai.SpawnAt(ent.Comp.FireEffect, grid, tile);
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                if (HasComp<AshDrakeComponent>(mob))
                    continue;
                _ai.Damage(mob, "Heat", ent.Comp.MeteorDamage, ent);
                _ai.Ignite(mob);
            }
        });
    }

    private void FallStep(EntityUid fireball, int steps, float stepSize)
    {
        if (steps <= 0 || TerminatingOrDeleted(fireball))
            return;
        _transform.SetCoordinates(fireball, Transform(fireball).Coordinates.Offset(new Vector2(0f, -stepSize)));
        _ai.Schedule(null, 0.1f, () => FallStep(fireball, steps - 1, stepSize));
    }

    #endregion

    #region Lava swoop

    /// <summary>
    /// lava_swoop.Trigger: при ярости — пике с огненной ареной, иначе лужи лавы вокруг цели и пике.
    /// onFinished вызывается после приземления (Trigger в SS13 возвращается только после всей атаки).
    /// </summary>
    private bool TryLavaSwoop(Entity<AshDrakeComponent> ent, EntityUid target, Action? onFinished)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        var enraged = Enraged(ent);
        if (!enraged)
            LavaPools(ent, target, ent.Comp.LavaPoolsAmount);

        SwoopAttack(ent, target, enraged, () =>
        {
            _ai.EndAbility(ent, ent.Comp.SwoopCooldown);
            onFinished?.Invoke();
        });
        return true;
    }

    private void SwoopAttack(Entity<AshDrakeComponent> ent, EntityUid target, bool lavaArena, Action onFinished)
    {
        var uid = ent.Owner;
        ent.Comp.Swooping = true;
        _ai.SetImmobile(uid, true);
        if (TryComp<MegafaunaAiComponent>(uid, out var ai))
            ai.Invulnerable = true;
        _physics.SetCanCollide(uid, false);
        _popup.PopupEntity(Loc.GetString("ash-drake-swoop-up", ("drake", uid)), uid, PopupType.LargeCaution);

        // start_attack: тень на земле, сам дракон улетает вверх.
        _appearance.SetData(uid, MegafaunaVisuals.State, "dragon_shadow");
        _appearance.SetData(uid, MegafaunaVisuals.Color, Color.White.WithAlpha(204 / 255f));
        var flight = Spawn(ent.Comp.Flight, Transform(uid).Coordinates);
        var negative = TryComp(target, out TransformComponent? targetXform) &&
                       _transform.GetWorldPosition(targetXform).X < _transform.GetWorldPosition(uid).X;
        AnimateFlight(flight, negative ? -1 : 1, 10);

        _ai.Schedule(uid, 0.3f, () =>
        {
            _appearance.SetData(uid, MegafaunaVisuals.Color, Color.White.WithAlpha(100 / 255f));
            _ai.Schedule(uid, 0.7f, () => SwoopChase(ent, target, lavaArena, onFinished));
        });
    }

    private void AnimateFlight(EntityUid flight, int dirX, int steps)
    {
        if (steps <= 0 || TerminatingOrDeleted(flight))
            return;
        var coords = Transform(flight).Coordinates.Offset(new Vector2(dirX, 1f));
        _transform.SetCoordinates(flight, coords);
        _ai.Schedule(null, 0.1f, () => AnimateFlight(flight, dirX, steps - 1));
    }

    /// <summary>Летит к клетке цели со скоростью клетка за 0.05 с, следуя за ней.</summary>
    private void SwoopChase(Entity<AshDrakeComponent> ent, EntityUid target, bool lavaArena, Action onFinished)
    {
        var uid = ent.Owner;
        if (!TerminatingOrDeleted(target) &&
            _ai.TryGetTile(uid, out var grid, out var ours) &&
            _ai.TryGetTile(target, out var targetGrid, out var theirs) &&
            grid.Owner == targetGrid.Owner &&
            ours != theirs)
        {
            var next = ours + MegafaunaAiSystem.StepTowards(ours, theirs);
            _transform.SetCoordinates(uid, _ai.TileCenter(grid, next));
            _ai.Schedule(uid, ent.Comp.SwoopStepDelay, () => SwoopChase(ent, target, lavaArena, onFinished));
            return;
        }

        if (lavaArena)
            LavaArena(ent, target, success => SwoopLand(ent, success, onFinished));
        else
            SwoopLand(ent, true, onFinished);
    }

    private void SwoopLand(Entity<AshDrakeComponent> ent, bool lavaSuccess, Action onFinished)
    {
        var uid = ent.Owner;
        var coords = Transform(uid).Coordinates;
        Spawn(ent.Comp.Landing, coords);
        _appearance.SetData(uid, MegafaunaVisuals.State, "dragon_swoop");
        _appearance.SetData(uid, MegafaunaVisuals.Color, Color.White);

        _ai.Schedule(uid, ent.Comp.SwoopDescentTime, () =>
        {
            _ai.PlaySound(ent.Comp.ImpactSound, Transform(uid).Coordinates, 5f);
            if (_ai.TryGetTile(uid, out var grid, out var center))
            {
                foreach (var tile in MegafaunaAiSystem.RangeTiles(center, 1))
                {
                    foreach (var victim in _ai.MobsOnTile(grid, tile))
                    {
                        if (victim == uid)
                            continue;

                        _ai.Damage(victim, "Blunt", ent.Comp.SwoopDamage, uid);
                        if (TerminatingOrDeleted(victim))
                            continue;

                        var dir = tile == center
                            ? new Vector2(_random.Next(-1, 2), _random.Next(-1, 2))
                            : new Vector2(tile.X - center.X, tile.Y - center.Y);
                        if (dir == Vector2.Zero)
                            dir = Vector2.UnitY;
                        _throwing.TryThrow(victim, Vector2.Normalize(dir) * 3f, 10f, uid);
                        _popup.PopupEntity(Loc.GetString("ash-drake-thrown", ("victim", victim), ("drake", uid)), victim, PopupType.MediumCaution);
                    }
                }
            }

            _ai.ShakeCamera(Transform(uid).Coordinates, 7f, 15f);
            if (TryComp<MegafaunaAiComponent>(uid, out var ai))
                ai.Invulnerable = false;
            _physics.SetCanCollide(uid, true);

            _ai.Schedule(uid, 0.1f, () =>
            {
                ent.Comp.Swooping = false;
                _ai.SetImmobile(uid, false);
                _appearance.SetData(uid, MegafaunaVisuals.State, string.Empty);
                onFinished();
                if (!lavaSuccess)
                    ArenaEscapeEnrage(ent);
            });
        });
    }

    /// <summary>lava_pools: 30 предупреждений о лаве вокруг цели, лава держится 6 с.</summary>
    private void LavaPools(Entity<AshDrakeComponent> ent, EntityUid target, int amount)
    {
        if (amount == ent.Comp.LavaPoolsAmount)
            _popup.PopupEntity(Loc.GetString("ash-drake-lava-pools"), target, target, PopupType.LargeCaution);

        if (amount <= 0 || TerminatingOrDeleted(target) || !_ai.TryGetTile(target, out var grid, out var center))
            return;

        var tiles = MegafaunaAiSystem.RangeTiles(center, 1).ToList();
        LavaWarning(ent, grid, _random.Pick(tiles), 6f);
        _ai.Schedule(ent, ent.Comp.LavaPoolsDelay, () => LavaPools(ent, target, amount - 1));
    }

    /// <summary>temp_visual/lava_warning: через 1.3 с огонь, 10 урона и временная лава.</summary>
    private void LavaWarning(Entity<AshDrakeComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, float resetTime)
    {
        var coords = _ai.TileCenter(grid, tile);
        _ai.SpawnAt(ent.Comp.LavaWarning, grid, tile);
        _ai.PlaySound(ent.Comp.WarnSound, coords);

        _ai.Schedule(null, ent.Comp.LavaWarningTime, () =>
        {
            _ai.PlaySound(ent.Comp.FireSound, coords, 5f);
            var canTransform = !_ai.IsBlocked(grid, tile) && !HasLava(grid, tile);

            foreach (var victim in _ai.MobsOnTile(grid, tile))
            {
                if (HasComp<AshDrakeComponent>(victim) || victim == ent.Owner)
                    continue;
                _ai.Damage(victim, "Heat", ent.Comp.LavaWarningDamage, ent);
                _popup.PopupEntity(Loc.GetString(canTransform ? "ash-drake-lava-fall" : "ash-drake-fireball-hit"), victim, victim, PopupType.LargeCaution);
            }

            if (!canTransform)
            {
                _ai.SpawnAt(ent.Comp.FireEffect, grid, tile);
                return;
            }

            var lava = _ai.SpawnAt(ent.Comp.Lava, grid, tile);
            EnsureComp<TimedDespawnComponent>(lava).Lifetime = resetTime;
        });
    }

    private bool HasLava(Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var e in _map.GetAnchoredEntities(grid, grid.Comp, tile))
        {
            if (HasComp<TileEntityEffectComponent>(e))
                return true;
        }

        return false;
    }

    /// <summary>
    /// lava_arena: огненная стена радиусом 3, трижды заливает арену лавой, оставляя каждому игроку
    /// безопасную клетку на расстоянии 2. Если игрок сбежал — арена провалена.
    /// </summary>
    private void LavaArena(Entity<AshDrakeComponent> ent, EntityUid target, Action<bool> onDone)
    {
        var uid = ent.Owner;
        if (TerminatingOrDeleted(target) || !_ai.TryGetTile(uid, out var grid, out var center))
        {
            onDone(true);
            return;
        }

        _popup.PopupEntity(Loc.GetString("ash-drake-arena", ("drake", uid)), target, target, PopupType.LargeCaution);

        var walls = new List<EntityUid>();
        foreach (var tile in MegafaunaAiSystem.RangeTiles(center, 3))
        {
            if (MegafaunaAiSystem.Chebyshev(tile, center) == 3)
                walls.Add(_ai.SpawnAt(ent.Comp.ArenaWall, grid, tile));
        }

        var arena = MegafaunaAiSystem.RangeTiles(center, 2).ToList();
        foreach (var tile in arena)
            _ai.Drill(grid, tile, uid);

        var withClients = new HashSet<EntityUid>();
        _ai.Schedule(uid, 1f, () => ArenaRound(ent, grid, arena, walls, withClients, 3, onDone));
    }

    private void ArenaRound(Entity<AshDrakeComponent> ent, Entity<MapGridComponent> grid, List<Vector2i> arena,
        List<EntityUid> walls, HashSet<EntityUid> withClients, int amount, Action<bool> onDone)
    {
        if (amount <= 0)
        {
            onDone(true);
            return;
        }

        var safe = new HashSet<Vector2i>();
        var anyAttack = false;
        foreach (var tile in arena)
        {
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                if (!HasComp<ActorComponent>(mob) || !_ai.TryGetTile(mob, out _, out var mobTile))
                    continue;

                var options = arena.Where(t => MegafaunaAiSystem.Chebyshev(t, mobTile) == 2 && !safe.Contains(t)).ToList();
                if (options.Count > 0)
                    safe.Add(_random.Pick(options));
                anyAttack = true;
                withClients.Add(mob);
            }
        }

        if (!anyAttack)
        {
            foreach (var wall in walls)
                QueueDel(wall);
            onDone(!withClients.Any(m => !TerminatingOrDeleted(m) && HasComp<ActorComponent>(m)));
            return;
        }

        foreach (var tile in arena)
        {
            if (!safe.Contains(tile))
                LavaWarning(ent, grid, tile, 1f);
            else
                _ai.SpawnAt(ent.Comp.LavaSafe, grid, tile);
        }

        _ai.Schedule(ent, 2.4f, () => ArenaRound(ent, grid, arena, walls, withClients, amount - 1, onDone));
    }

    /// <summary>arena_escape_enrage: лечение 250, ускорение вдвое, через 5 с круговое пламя.</summary>
    private void ArenaEscapeEnrage(Entity<AshDrakeComponent> ent)
    {
        var uid = ent.Owner;
        _popup.PopupEntity(Loc.GetString("ash-drake-arena-escape", ("drake", uid)), uid, PopupType.LargeCaution);
        _ai.Heal(uid, ent.Comp.ArenaEscapeHeal);
        _appearance.SetData(uid, MegafaunaVisuals.Color, EnrageColor);
        _ai.SetSpeedMultiplier(uid, 2f);
        if (_light.TryGetLight(uid, out var light))
            _light.SetRadius(uid, 10f, light);

        _ai.Schedule(uid, 5f, () =>
        {
            if (_ai.GetTarget(uid) is { } target)
                MassFire(ent, target);
            _ai.EndAbility(uid, 8f);
            _ai.SetSpeedMultiplier(uid, 1f);
            _appearance.SetData(uid, MegafaunaVisuals.Color, Color.White);
            if (_light.TryGetLight(uid, out var l))
                _light.SetRadius(uid, 3f, l);
        });
    }

    #endregion
}
