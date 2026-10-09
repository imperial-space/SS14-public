using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.NPC.HTN;
using Content.Shared.Damage.Systems;
using Content.Shared.Fluids.Components;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Bubblegum;

/// <summary>
/// Перенос megafauna/bubblegum из SS13: OpenFire, bloodattack (bloodsmack/bloodgrab), blood_warp + blood_enrage,
/// triple_charge, hallucination_charge, hallucination_surround, blood_walk и отражение снарядов в ярости.
/// </summary>
public sealed class BubblegumSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    private const string AggressiveKey = "MegafaunaAggressive";
    private static readonly Color BubblegumRed = Color.FromHex("#950A0A");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BubblegumComponent, MegafaunaOpenFireEvent>(OnOpenFire);
        SubscribeLocalEvent<BubblegumComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<BubblegumComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BubblegumComponent, ProjectileReflectAttemptEvent>(OnProjectile);
        SubscribeLocalEvent<BubblegumComponent, HitScanReflectAttemptEvent>(OnHitscan);
        SubscribeLocalEvent<BubblegumComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BubblegumComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // update_approach: держится в 5 клетках, пока не в ярости и цель не обездвижена.
        var query = EntityQueryEnumerator<BubblegumComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            if (comp.IsHallucination)
                continue;

            var aggressive = IsEnraged(comp) || _ai.GetTarget(uid) is { } target && IsIncapacitated(target);
            var has = htn.Blackboard.ContainsKey(AggressiveKey);
            if (aggressive == has)
                continue;

            if (aggressive)
                htn.Blackboard.SetValue(AggressiveKey, true);
            else
                htn.Blackboard.Remove<bool>(AggressiveKey);
            _htn.Replan(htn);
        }
    }

    private bool IsEnraged(BubblegumComponent comp)
    {
        return comp.EnrageTill > _timing.CurTime;
    }

    private bool IsIncapacitated(EntityUid target)
    {
        return !_mobState.IsAlive(target) || HasComp<StunnedComponent>(target) || HasComp<KnockedDownComponent>(target);
    }

    private bool Smash(EntityUid uid)
    {
        return _ai.GetHealth(uid) <= _ai.GetMaxHealth(uid) * 0.5f;
    }

    #region Events

    private void OnOpenFire(Entity<BubblegumComponent> ent, ref MegafaunaOpenFireEvent args)
    {
        if (ent.Comp.IsHallucination)
            return;

        var target = args.Target;
        var anger = _ai.Anger(ent, 60f, 20f);

        void Charge()
        {
            if (!_ai.IsAliveBoss(ent))
                return;
            if (!Smash(ent))
                TryTripleCharge(ent, target);
            else if (_ai.Prob(50 + anger))
                TryHallucinationCharge(ent, target);
            else
                TrySurround(ent, target);
        }

        if (!TryBloodAttack(ent) || _ai.Prob(25 + anger))
            TryBloodWarp(ent, target, Charge);
        else
            Charge();
    }

    /// <summary>blood_walk: кровавый след и грохот шагов.</summary>
    private void OnMove(Entity<BubblegumComponent> ent, ref MoveEvent args)
    {
        if (!_ai.TryGetTile(ent, out var grid, out var tile) || ent.Comp.LastTile == tile)
            return;

        ent.Comp.LastTile = tile;
        _ai.PlaySound(ent.Comp.StepSound, _ai.TileCenter(grid, tile), 5f);
        if (!GetBloodPools(grid, tile, 0).Any())
            _ai.SpawnAt(ent.Comp.BloodDecal, grid, tile);
    }

    /// <summary>adjust_brute_loss: anger, enrage_time и 25% шанс брызг крови.</summary>
    private void OnDamageChanged(Entity<BubblegumComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.IsHallucination || args.DamageDelta == null || !args.DamageIncreased)
            return;

        var brute = args.DamageDelta.DamageDict
            .Where(d => d.Key.Id is "Blunt" or "Slash" or "Piercing")
            .Sum(d => d.Value.Float());
        if (brute <= 0)
            return;

        var anger = _ai.Anger(ent, 60f, 20f);
        ent.Comp.EnrageTime = 7f * Math.Clamp(anger / 20f, 0.5f, 1f);

        if (!_random.Prob(0.25f) || !_ai.TryGetTile(ent, out var grid, out var tile))
            return;

        if (_random.Prob(0.4f))
            tile += _random.Pick(new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right });
        _ai.SpawnAt(ent.Comp.BloodGibs, grid, tile);
    }

    /// <summary>projectile_hit: в ярости отражает снаряды.</summary>
    private void OnProjectile(Entity<BubblegumComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (!IsEnraged(ent.Comp))
            return;

        args.Cancelled = true;
        QueueDel(args.ProjUid);
        Deflect(ent);
    }

    private void OnHitscan(Entity<BubblegumComponent> ent, ref HitScanReflectAttemptEvent args)
    {
        if (!IsEnraged(ent.Comp))
            return;

        args.Reflected = true;
        Deflect(ent);
    }

    private void Deflect(Entity<BubblegumComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("bubblegum-deflect", ("boss", ent.Owner)), ent, PopupType.MediumCaution);
        _ai.PlaySound(ent.Comp.DeflectSound, Transform(ent).Coordinates, 5f);
    }

    private void OnMobStateChanged(Entity<BubblegumComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !ent.Comp.IsHallucination)
            return;

        // hallucination: "Explodes into a pool of blood!"
        _popup.PopupEntity(Loc.GetString("bubblegum-hallucination-death"), ent, PopupType.Medium);
        QueueDel(ent);
    }

    private void OnTerminating(Entity<BubblegumComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.IsHallucination && _ai.TryGetTile(ent, out var grid, out var tile))
            _ai.SpawnAt(ent.Comp.BloodDecal, grid, tile);
    }

    #endregion

    #region Blood

    /// <summary>get_bloodcrawlable_pools: кровь в радиусе range клеток.</summary>
    private List<EntityUid> GetBloodPools(Entity<MapGridComponent> grid, Vector2i center, int range)
    {
        var result = new List<EntityUid>();
        var coords = _ai.TileCenter(grid, center);
        foreach (var ent in _lookup.GetEntitiesInRange(coords, range * 1.5f + 0.5f))
        {
            if (!HasComp<BubblegumBloodComponent>(ent) && !IsBloodPuddle(ent))
                continue;
            if (!_ai.TryGetTile(ent, out var entGrid, out var tile) || entGrid.Owner != grid.Owner)
                continue;
            if (MegafaunaAiSystem.Chebyshev(tile, center) <= range)
                result.Add(ent);
        }

        return result;
    }

    private bool IsBloodPuddle(EntityUid uid)
    {
        if (!TryComp<PuddleComponent>(uid, out var puddle) || puddle.Solution is not { } solution)
            return false;
        return solution.Comp.Solution.Contents.Any(r => r.Reagent.Prototype.Contains("Blood"));
    }

    /// <summary>get_mobs_on_blood: цели в зоне видимости, стоящие в крови.</summary>
    private List<EntityUid> GetMobsOnBlood(EntityUid uid)
    {
        var result = new List<EntityUid>();
        if (!TryComp<MegafaunaAiComponent>(uid, out var ai))
            return result;

        foreach (var mob in _lookup.GetEntitiesInRange<Content.Shared.Mobs.Components.MobStateComponent>(Transform(uid).Coordinates, ai.AggroRange))
        {
            if (mob.Owner == uid || HasComp<BubblegumComponent>(mob) || HasComp<MegafaunaAiComponent>(mob))
                continue;
            if (!_ai.TryGetTile(mob, out var grid, out var tile))
                continue;
            if (GetBloodPools(grid, tile, 0).Count > 0)
                result.Add(mob);
        }

        return result;
    }

    private bool TryBloodAttack(Entity<BubblegumComponent> ent)
    {
        var targets = GetMobsOnBlood(ent);
        if (targets.Count == 0)
            return false;

        BloodAttack(ent, targets, _random.Prob(0.5f));
        return true;
    }

    /// <summary>bloodattack: до двух рук из крови — шлепок или захват.</summary>
    private void BloodAttack(Entity<BubblegumComponent> ent, List<EntityUid> targets, bool handedness)
    {
        var targetOne = _random.PickAndTake(targets);
        EntityUid? targetTwo = targets.Count > 0 ? _random.PickAndTake(targets) : null;

        void HitOne(bool hand, Action next)
        {
            if (TerminatingOrDeleted(targetOne) || !_ai.TryGetTile(targetOne, out var grid, out var tile) ||
                GetBloodPools(grid, tile, 0).Count == 0)
            {
                next();
                return;
            }

            if (!_mobState.IsAlive(targetOne) || _random.Prob(0.1f))
                BloodGrab(ent, grid, tile, hand, next);
            else
                BloodSmack(ent, grid, tile, hand, next);
        }

        Action afterOne = () =>
        {
            if (targetTwo == null)
                HitOne(handedness, () => { });
        };

        if (targetTwo is { } two && _ai.TryGetTile(two, out var g2, out var t2))
        {
            if (!_mobState.IsAlive(two) || _random.Prob(0.1f))
                BloodGrab(ent, g2, t2, handedness, () => HitOne(!handedness, afterOne));
            else
                BloodSmack(ent, g2, t2, handedness, () => HitOne(!handedness, afterOne));
            return;
        }

        HitOne(!handedness, afterOne);
    }

    /// <summary>bloodsmack: через 0.4 с 10 урона всем на клетке.</summary>
    private void BloodSmack(Entity<BubblegumComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, bool right, Action next)
    {
        _ai.SpawnAt(right ? ent.Comp.RightSmack : ent.Comp.LeftSmack, grid, tile);
        _ai.Schedule(ent, 0.4f, () =>
        {
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                if (HasComp<BubblegumComponent>(mob))
                    continue;
                _popup.PopupEntity(Loc.GetString("bubblegum-rends-you", ("boss", ent.Owner)), mob, mob, PopupType.LargeCaution);
                _ai.PlaySound(ent.Comp.AttackSound, _ai.TileCenter(grid, tile));
                _ai.Damage(mob, "Slash", ent.Comp.SmackDamage, ent);
            }

            _ai.Schedule(ent, 0.3f, next);
        });
    }

    /// <summary>bloodgrab: через 0.6 с утаскивает беспомощных к себе и пожирает.</summary>
    private void BloodGrab(Entity<BubblegumComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, bool right, Action next)
    {
        _ai.SpawnAt(right ? ent.Comp.RightGrab : ent.Comp.LeftGrab, grid, tile);
        _ai.Schedule(ent, 0.6f, () =>
        {
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                if (HasComp<BubblegumComponent>(mob) || _mobState.IsAlive(mob))
                    continue;

                _popup.PopupEntity(Loc.GetString("bubblegum-drags-you", ("boss", ent.Owner)), mob, mob, PopupType.LargeCaution);
                _ai.PlaySound(ent.Comp.EnterBloodSound, _ai.TileCenter(grid, tile));
                if (_ai.TryGetTile(ent, out var ownGrid, out var ownTile))
                {
                    var front = ownTile + MegafaunaAiSystem.StepTowards(ownTile, tile);
                    _transform.SetCoordinates(mob, _ai.TileCenter(ownGrid, front));
                    _ai.PlaySound(ent.Comp.ExitBloodSound, _ai.TileCenter(ownGrid, front));
                }

                var victim = mob;
                _ai.Schedule(ent, 0.2f, () => _ai.Devour(ent, victim));
            }

            _ai.Schedule(ent, 0.1f, next);
        });
    }

    /// <summary>blood_warp: прыжок в лужу крови в 5 клетках от цели, затем ярость.</summary>
    private void TryBloodWarp(Entity<BubblegumComponent> ent, EntityUid target, Action then)
    {
        if (!_ai.TryBeginAbility(ent))
        {
            then();
            return;
        }

        void Finish()
        {
            _ai.EndAbility(ent, 0f);
            then();
        }

        if (_ai.TileDistance(ent, target) <= 1 ||
            !_ai.TryGetTile(ent, out var grid, out var ownTile) ||
            !_ai.TryGetTile(target, out var targetGrid, out var targetTile) ||
            grid.Owner != targetGrid.Owner ||
            GetBloodPools(grid, ownTile, 1).Count == 0 ||
            WarpPools(ent, grid, targetTile).Count == 0)
        {
            Finish();
            return;
        }

        Spawn(ent.Comp.Decoy, Transform(ent).Coordinates);
        _ai.Schedule(ent, 0.3f, () =>
        {
            var pools = WarpPools(ent, grid, targetTile);
            if (pools.Count > 0)
            {
                var pool = _random.Pick(pools);
                _popup.PopupEntity(Loc.GetString("bubblegum-sinks"), ent, PopupType.MediumCaution);
                _ai.PlaySound(ent.Comp.EnterBloodSound, Transform(ent).Coordinates);
                _transform.SetCoordinates(ent, Transform(pool).Coordinates);
                _ai.PlaySound(ent.Comp.ExitBloodSound, Transform(ent).Coordinates);
                _popup.PopupEntity(Loc.GetString("bubblegum-springs-out"), ent, PopupType.MediumCaution);
                BloodEnrage(ent);
            }

            Finish();
        });
    }

    private List<EntityUid> WarpPools(Entity<BubblegumComponent> ent, Entity<MapGridComponent> grid, Vector2i targetTile)
    {
        var range = ent.Comp.BloodWarpRange;
        var inner = GetBloodPools(grid, targetTile, range - 1).ToHashSet();
        return GetBloodPools(grid, targetTile, range).Where(p => !inner.Contains(p)).ToList();
    }

    /// <summary>blood_enrage: быстрее, неуязвим к снарядам, идёт вплотную.</summary>
    private void BloodEnrage(Entity<BubblegumComponent> ent)
    {
        var now = _timing.CurTime;
        var enrageTime = TimeSpan.FromSeconds(ent.Comp.EnrageTime);
        if (ent.Comp.EnrageTill + enrageTime * 2 > now)
            return;

        ent.Comp.EnrageTill = now + enrageTime;
        _ai.SetSpeedMultiplier(ent, ent.Comp.EnrageSpeedMultiplier);
        _appearance.SetData(ent, MegafaunaVisuals.Color, BubblegumRed);
        _ai.Schedule(ent, ent.Comp.EnrageTime, () =>
        {
            _ai.SetSpeedMultiplier(ent, 1f);
            _appearance.SetData(ent, MegafaunaVisuals.Color, Color.White);
        });
    }

    #endregion

    #region Charges

    private bool TryTripleCharge(Entity<BubblegumComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        _ai.SetImmobile(ent, true);
        // triple_charge: задержки 0.6, 0.4, 0.2 с.
        DoCharge(ent, ent, target, 0.6f, ent.Comp.ChargePast, () =>
            DoCharge(ent, ent, target, 0.4f, ent.Comp.ChargePast, () =>
                DoCharge(ent, ent, target, 0.2f, ent.Comp.ChargePast, () => EndCharges(ent, ent.Comp.ChargeCooldown))));
        return true;
    }

    private bool TryHallucinationCharge(Entity<BubblegumComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        _ai.SetImmobile(ent, true);
        var cooldown = ent.Comp.HallucinationCooldown;
        if (!Smash(ent) || _ai.Prob(33))
        {
            HallucinationCharge(ent, target, 6, 0.8f, 0, 6, true, () => EndCharges(ent, cooldown));
            return true;
        }

        HallucinationCharge(ent, target, 4, 0.9f, 0, 4, true, () =>
            HallucinationCharge(ent, target, 4, 0.7f, 0, 4, true, () =>
                HallucinationCharge(ent, target, 4, 0.5f, 0, 4, true, () =>
                    DoCharge(ent, ent, target, 0.6f, ent.Comp.ChargePast, () =>
                        DoCharge(ent, ent, target, 0.4f, ent.Comp.ChargePast, () =>
                            DoCharge(ent, ent, target, 0.2f, ent.Comp.ChargePast, () => EndCharges(ent, cooldown)))))));
        return true;
    }

    /// <summary>hallucination_surround: 5 раз два клона с боков и сам Пузырь.</summary>
    private bool TrySurround(Entity<BubblegumComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        _ai.SetImmobile(ent, true);
        SurroundStep(ent, target, 5);
        return true;
    }

    private void SurroundStep(Entity<BubblegumComponent> ent, EntityUid target, int remaining)
    {
        if (remaining <= 0)
        {
            EndCharges(ent, ent.Comp.HallucinationCooldown);
            return;
        }

        HallucinationCharge(ent, target, 2, 0.8f, 2, 2, false, () => { });
        DoCharge(ent, ent, target, 0.6f, ent.Comp.ChargePast, () => SurroundStep(ent, target, remaining - 1));
    }

    private void EndCharges(Entity<BubblegumComponent> ent, float cooldown)
    {
        _ai.SetImmobile(ent, false);
        _ai.EndAbility(ent, cooldown);
    }

    /// <summary>
    /// hallucination_charge: клоны по кругу радиусом radius вокруг цели; при useSelf сам Пузырь
    /// встаёт на первое место и бежит вместе с ними.
    /// </summary>
    private void HallucinationCharge(Entity<BubblegumComponent> ent, EntityUid target, int amount, float delay, int past, int radius, bool useSelf, Action onDone)
    {
        if (TerminatingOrDeleted(target) || !_ai.TryGetTile(target, out var grid, out var center))
        {
            onDone();
            return;
        }

        var startAngle = _random.Next(1, 361);
        var step = 360f / amount;
        var selfPlaced = false;
        for (var i = 1; i <= amount; i++)
        {
            var rad = (startAngle + step * i) * MathF.PI / 180f;
            var place = center + new Vector2i((int) MathF.Round(MathF.Cos(rad) * radius), (int) MathF.Round(MathF.Sin(rad) * radius));
            if (useSelf && !selfPlaced)
            {
                _transform.SetCoordinates(ent, _ai.TileCenter(grid, place));
                selfPlaced = true;
                continue;
            }

            var clone = _ai.SpawnAt(ent.Comp.Hallucination, grid, place);
            DoCharge(ent, clone, target, delay, past, () => QueueDel(clone));
        }

        if (useSelf)
            DoCharge(ent, ent, target, delay, past, onDone);
        else
            onDone();
    }

    /// <summary>
    /// do_charge: метка на клетке за целью, пауза delay, затем рывок по клетке за 0.05 с.
    /// Сбивает всех на пути (30 урона, клоны — 15), прорывает породу.
    /// </summary>
    private void DoCharge(Entity<BubblegumComponent> owner, EntityUid charger, EntityUid target, float delay, int past, Action onDone)
    {
        if (TerminatingOrDeleted(target) || TerminatingOrDeleted(charger) ||
            !_ai.TryGetTile(charger, out var grid, out var from) ||
            !_ai.TryGetTile(target, out var targetGrid, out var targetTile) ||
            grid.Owner != targetGrid.Owner)
        {
            onDone();
            return;
        }

        var dir = MegafaunaAiSystem.StepTowards(from, targetTile);
        var end = targetTile + dir * past;
        _ai.SpawnAt(owner.Comp.ChargeMarker, grid, end);
        Spawn(owner.Comp.Decoy, Transform(charger).Coordinates);

        var damage = charger == owner.Owner ? owner.Comp.ChargeDamage : owner.Comp.HallucinationDamage;
        _ai.Schedule(owner, delay, () =>
        {
            if (TerminatingOrDeleted(charger) || !_ai.TryGetTile(charger, out _, out var start))
            {
                onDone();
                return;
            }

            var steps = Math.Min(MegafaunaAiSystem.Chebyshev(start, end), 50);
            ChargeStep(owner, charger, grid, end, steps, damage, new HashSet<EntityUid> { charger, owner }, onDone);
        });
    }

    private void ChargeStep(Entity<BubblegumComponent> owner, EntityUid charger, Entity<MapGridComponent> grid, Vector2i end,
        int remaining, float damage, HashSet<EntityUid> hit, Action onDone)
    {
        if (remaining <= 0 || TerminatingOrDeleted(charger) || !_ai.TryGetTile(charger, out _, out var current) || current == end)
        {
            // COMSIG_FINISHED_CHARGE -> after_charge -> try_bloodattack
            if (_ai.IsAliveBoss(owner))
                TryBloodAttack(owner);
            _ai.Schedule(owner, owner.Comp.ChargeStepDelay, onDone);
            return;
        }

        var next = current + MegafaunaAiSystem.StepTowards(current, end);
        if (_ai.IsMineral(grid, next))
            _ai.Drill(grid, next, owner);
        else if (_ai.IsBlocked(grid, next))
            remaining = 1;

        foreach (var mob in _ai.MobsOnTile(grid, next))
        {
            if (HasComp<BubblegumComponent>(mob) || !hit.Add(mob))
                continue;

            _popup.PopupEntity(Loc.GetString("bubblegum-tramples", ("boss", owner.Owner)), mob, mob, PopupType.LargeCaution);
            _ai.Damage(mob, "Blunt", damage, owner);
            _ai.PlaySound(owner.Comp.StepSound, Transform(mob).Coordinates);
            _ai.ShakeCamera(Transform(mob).Coordinates, 0.5f, 4f);
        }

        if (!_ai.IsBlocked(grid, next))
        {
            Spawn(owner.Comp.DecoyFading, Transform(charger).Coordinates);
            _transform.SetCoordinates(charger, _ai.TileCenter(grid, next));
        }

        _ai.Schedule(owner, owner.Comp.ChargeStepDelay, () => ChargeStep(owner, charger, grid, end, remaining - 1, damage, hit, onDone));
    }

    #endregion
}
