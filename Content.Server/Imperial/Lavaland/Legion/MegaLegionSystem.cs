using System.Linq;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.NPC.HTN;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.Legion;

/// <summary>
/// Перенос megafauna/legion из SS13: OpenFire (rand(4)), create_legion_skull, chase_target,
/// create_legion_turrets, throw_impact и Split при смерти.
/// </summary>
public sealed class MegaLegionSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    private const string AggressiveKey = "MegafaunaAggressive";
    private static readonly Vector2i[] Cardinals = { Vector2i.Up, Vector2i.Down, Vector2i.Right, Vector2i.Left };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegaLegionComponent, MegafaunaOpenFireEvent>(OnOpenFire);
        SubscribeLocalEvent<MegaLegionComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnOpenFire(Entity<MegaLegionComponent> ent, ref MegafaunaOpenFireEvent args)
    {
        switch (_random.Next(0, 5)) // Larger skulls use more attacks.
        {
            case <= 2:
                TrySkull(ent, args.Target);
                break;
            case 3:
                TryChase(ent, args.Target);
                break;
            default:
                TryTurrets(ent, args.Target);
                break;
        }
    }

    /// <summary>create_legion_skull: череп у ног, сразу атакует цель.</summary>
    private void TrySkull(Entity<MegaLegionComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return;

        Spawn(ent.Comp.Brood, Transform(ent).Coordinates);
        _ai.EndAbility(ent, ent.Comp.SkullCooldown);
    }

    /// <summary>
    /// chase_target: «charges!», 6 с идёт вплотную и крутится, через 2 с бросается на цель
    /// на 7 клеток (удар 22 * size / 2 и отброс).
    /// </summary>
    private void TryChase(Entity<MegaLegionComponent> ent, EntityUid target)
    {
        if (!_mobState.IsAlive(target) || !_ai.TryBeginAbility(ent))
            return;

        _ai.EndAbility(ent, ent.Comp.ChaseCooldown);
        _popup.PopupEntity(Loc.GetString("legion-megafauna-charges", ("boss", ent.Owner)), ent, PopupType.LargeCaution);
        SetAggressive(ent, true);
        ent.Comp.Charging = true;
        Spin(ent, 60);

        _ai.Schedule(ent, 2f, () => ThrowSelf(ent, target));
        _ai.Schedule(ent, 6f, () =>
        {
            ent.Comp.Charging = false;
            SetAggressive(ent, false);
            _transform.SetLocalRotation(ent, Angle.Zero);
        });
    }

    private void Spin(Entity<MegaLegionComponent> ent, int steps)
    {
        if (steps <= 0 || !ent.Comp.Charging)
            return;
        _transform.SetLocalRotation(ent, Transform(ent).LocalRotation + Angle.FromDegrees(36));
        _ai.Schedule(ent, 0.1f, () => Spin(ent, steps - 1));
    }

    private void SetAggressive(EntityUid uid, bool aggressive)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;
        if (aggressive)
            htn.Blackboard.SetValue(AggressiveKey, true);
        else
            htn.Blackboard.Remove<bool>(AggressiveKey);
        _htn.Replan(htn);
    }

    /// <summary>throw_thyself: полёт до 7 клеток к цели, при ударе — урон и отброс.</summary>
    private void ThrowSelf(Entity<MegaLegionComponent> ent, EntityUid target)
    {
        _ai.PlaySound(ent.Comp.ChargeSound, Transform(ent).Coordinates);
        if (TerminatingOrDeleted(target) || !_ai.TryGetTile(ent, out var grid, out var from) ||
            !_ai.TryGetTile(target, out var targetGrid, out var to) || grid.Owner != targetGrid.Owner)
            return;

        ThrowStep(ent, grid, to, 7);
    }

    private void ThrowStep(Entity<MegaLegionComponent> ent, Entity<MapGridComponent> grid, Vector2i end, int remaining)
    {
        if (remaining <= 0 || !_ai.TryGetTile(ent, out _, out var current) || current == end)
            return;

        var step = MegafaunaAiSystem.StepTowards(current, end);
        var next = current + step;
        if (_ai.IsBlocked(grid, next))
        {
            _ai.PlaySound(ent.Comp.ImpactSound, Transform(ent).Coordinates);
            return;
        }

        var victims = _ai.MobsOnTile(grid, next).Where(m => !HasComp<MegaLegionComponent>(m)).ToList();
        if (victims.Count > 0)
        {
            _ai.PlaySound(ent.Comp.ImpactSound, Transform(ent).Coordinates);
            foreach (var victim in victims)
            {
                _ai.PlaySound(ent.Comp.AttackSound, Transform(victim).Coordinates);
                _ai.Damage(victim, "Blunt", 22f * ent.Comp.Size / 2f, ent);
                var knock = next + step * 2;
                if (!_ai.IsBlocked(grid, knock) && !_ai.IsBlocked(grid, next + step))
                    _transform.SetCoordinates(victim, _ai.TileCenter(grid, knock));
            }

            return;
        }

        _transform.SetCoordinates(ent, _ai.TileCenter(grid, next));
        _ai.Schedule(ent, 0.05f, () => ThrowStep(ent, grid, end, remaining - 1));
    }

    /// <summary>create_legion_turrets: 2..size*2 стражей на свободных клетках в 4 от легиона.</summary>
    private void TryTurrets(Entity<MegaLegionComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return;

        _ai.PlaySound(ent.Comp.TurretSummonSound, Transform(ent).Coordinates, 3f);
        if (_ai.TryGetTile(ent, out var grid, out var center))
        {
            var possible = MegafaunaAiSystem.RangeTiles(center, 4)
                .Where(t => t != center && !_ai.IsBlocked(grid, t))
                .ToList();
            var count = Math.Min(_random.Next(ent.Comp.MinTurrets, ent.Comp.Size * 2 + 1), possible.Count);
            for (var i = 0; i < count; i++)
            {
                var tile = _random.PickAndTake(possible);
                var turret = _ai.SpawnAt(ent.Comp.Turret, grid, tile);
                EnsureComp<LegionTurretComponent>(turret).Owner = ent;
                _ai.Schedule(null, ent.Comp.TurretInitialDelay, () => TurretSetUpShot(ent.Comp, turret, grid));
            }
        }

        _ai.EndAbility(ent, ent.Comp.TurretCooldown);
    }

    /// <summary>set_up_shot: первая живая цель в 9 клетках, иначе в случайную сторону.</summary>
    private void TurretSetUpShot(MegaLegionComponent comp, EntityUid turret, Entity<MapGridComponent> grid)
    {
        if (TerminatingOrDeleted(turret) || !_ai.TryGetTile(turret, out _, out var origin))
            return;

        Vector2i? aim = null;
        foreach (var tile in MegafaunaAiSystem.RangeTiles(origin, 9).OrderBy(t => MegafaunaAiSystem.Chebyshev(t, origin)))
        {
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                if (!_mobState.IsAlive(mob) || HasComp<MegaLegionComponent>(mob) || HasComp<MegafaunaAiComponent>(mob))
                    continue;
                aim = tile;
                break;
            }

            if (aim != null)
                break;
        }

        var dir = aim is { } a && a != origin
            ? new System.Numerics.Vector2(a.X - origin.X, a.Y - origin.Y)
            : (System.Numerics.Vector2) _random.Pick(Cardinals);
        dir = System.Numerics.Vector2.Normalize(dir);

        var line = MegafaunaAiSystem.Line(origin, origin + new Vector2i((int) MathF.Round(dir.X * 9), (int) MathF.Round(dir.Y * 9)))
            .Skip(1).ToList();
        foreach (var tile in line)
            _ai.SpawnAt(comp.TracerEffect, grid, tile);
        _ai.PlaySound(comp.TracerSound, Transform(turret).Coordinates, 3f);

        _ai.Schedule(null, comp.TurretShotDelay, () => TurretFire(comp, turret, grid, origin, dir));
    }

    /// <summary>fire_beam: кровавый импульс на 6 клеток, пробивает всех.</summary>
    private void TurretFire(MegaLegionComponent comp, EntityUid turret, Entity<MapGridComponent> grid, Vector2i origin, System.Numerics.Vector2 dir)
    {
        if (TerminatingOrDeleted(turret))
            return;

        var end = origin + new Vector2i((int) MathF.Round(dir.X * comp.TurretBeamRange), (int) MathF.Round(dir.Y * comp.TurretBeamRange));
        foreach (var tile in MegafaunaAiSystem.Line(origin, end).Skip(1))
        {
            if (_ai.IsBlocked(grid, tile))
                break;
            _ai.SpawnAt(comp.BeamEffect, grid, tile);
            foreach (var mob in _ai.MobsOnTile(grid, tile))
                _ai.Damage(mob, "Heat", comp.TurretDamage, turret);
        }

        _ai.PlaySound(comp.BeamSound, Transform(turret).Coordinates, 3f);
        _ai.Schedule(null, 0.5f, () => QueueDel(turret));
    }

    /// <summary>death + Split: делится на трёх меньших; последний легион оставляет посох бурь.</summary>
    private void OnMobStateChanged(Entity<MegaLegionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var child in ent.Comp.SplitInto)
            Spawn(child, coords);

        var lastLegion = true;
        var query = EntityQueryEnumerator<MegaLegionComponent>();
        while (query.MoveNext(out var other, out _))
        {
            if (other == ent.Owner || _mobState.IsDead(other))
                continue;
            lastLegion = false;
            break;
        }

        if (lastLegion)
            Spawn(ent.Comp.LastLoot, coords);
        else if (_random.Prob(0.2f))
            Spawn(ent.Comp.ExtraLoot, coords);
        else
        {
            for (var i = 0; i < ent.Comp.DefaultLootCount; i++)
                Spawn(ent.Comp.DefaultLoot, coords);
        }
    }
}
