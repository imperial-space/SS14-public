using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.Imperial.Lavaland.MegafaunaSleep;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.DeadMiner;

/// <summary>
/// Перенос basic/boss/blood_drunk_miner из SS13: ИИ (shoot_pka, dash_attack, melee),
/// do_chain_attack пилой, kinetic_accelerator, basic_charge и transform_weapon.
/// </summary>
public sealed class DeadMinerSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly Vector2i[] Dirs8 =
    {
        new(0, 1), new(1, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeadMinerComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<DeadMinerComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    /// <summary>Обычную атаку заменяет серия ударов пилой.</summary>
    private void OnAttemptMelee(Entity<DeadMinerComponent> ent, ref AttemptMeleeEvent args)
    {
        args.Cancelled = true;
    }

    /// <summary>death_effect: «распадается на светящиеся частицы».</summary>
    private void OnMobStateChanged(Entity<DeadMinerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        Spawn(ent.Comp.DeathEffect, Transform(ent).Coordinates);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<DeadMinerComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive || comp.Busy || HasComp<LavalandMegafaunaSleepComponent>(uid))
                continue;
            if (_ai.GetTarget(uid) is not { } target || _mobState.IsDead(target))
                continue;

            var ent = (uid, comp);
            var dist = _ai.TileDistance(uid, target);

            if (now >= comp.RangedReady)
            {
                // shoot_pka: ближе pka_range — очередь из ПКА.
                if (dist < comp.PkaRange && now >= comp.NextPka)
                {
                    comp.RangedReady = now + TimeSpan.FromSeconds(comp.RangedAttackCooldown);
                    TryTransform(ent);
                    FirePka(ent, target, () => { });
                    continue;
                }

                // dash_attack: дальше — рывок и очередь.
                if (dist >= comp.PkaRange && now >= comp.NextDash && now >= comp.NextPka)
                {
                    comp.RangedReady = now + TimeSpan.FromSeconds(comp.RangedAttackCooldown);
                    TryTransform(ent);
                    DashAttack(ent, target);
                    continue;
                }
            }

            if (dist <= 1 && now >= comp.NextMelee)
            {
                TryTransform(ent);
                ChainAttack(ent, target, 1);
            }
        }
    }

    /// <summary>transform_weapon: раскрыть/сложить пилу, откат 5-10 с.</summary>
    private void TryTransform(Entity<DeadMinerComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.NextTransform)
            return;

        ent.Comp.NextTransform = _timing.CurTime +
            TimeSpan.FromSeconds(_random.NextFloat(ent.Comp.TransformCooldownMin, ent.Comp.TransformCooldownMax));
        ent.Comp.SawOpen = !ent.Comp.SawOpen;
        _appearance.SetData(ent, MegafaunaVisuals.State, ent.Comp.SawOpen ? "miner_transformed" : string.Empty);
    }

    /// <summary>
    /// do_chain_attack: 5 ударов по 8 через 0.3 с (закрытая пила) или 3 удара по 12 через 0.5 с
    /// (раскрытая, задевает всех перед собой).
    /// </summary>
    private void ChainAttack(Entity<DeadMinerComponent> ent, EntityUid victim, int sequenceHit)
    {
        var hits = ent.Comp.SawOpen ? ent.Comp.OpenHits : ent.Comp.ClosedHits;
        if (_ai.TileDistance(ent, victim) > 1 || TerminatingOrDeleted(victim))
        {
            ent.Comp.NextMelee = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.MeleeCooldown);
            return;
        }

        ent.Comp.NextMelee = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.MeleeCooldown);
        _popup.PopupEntity(Loc.GetString("dead-miner-slashes", ("boss", ent.Owner)), victim, victim, PopupType.MediumCaution);
        _ai.PlaySound(ent.Comp.SlashSound, Transform(victim).Coordinates);

        var damage = ent.Comp.SawOpen ? ent.Comp.OpenDamage : ent.Comp.ClosedDamage;
        _ai.Damage(victim, "Slash", damage, ent);
        if (ent.Comp.SawOpen && _ai.TryGetTile(ent, out var grid, out var ours) && _ai.TryGetTile(victim, out _, out var theirs))
        {
            // Раскрытая пила бьёт дугой по трём клеткам перед собой.
            var dir = MegafaunaAiSystem.StepTowards(ours, theirs);
            var index = Array.IndexOf(Dirs8, dir);
            foreach (var tile in new[] { ours + dir, ours + Dirs8[(index + 1) % 8], ours + Dirs8[(index + 7) % 8] })
            {
                foreach (var mob in _ai.MobsOnTile(grid, tile))
                {
                    if (mob != victim && mob != ent.Owner && !_mobState.IsDead(mob) && !HasComp<MegafaunaAiComponent>(mob))
                        _ai.Damage(mob, "Slash", damage, ent);
                }
            }
        }

        if (sequenceHit >= hits)
            return;

        var delay = ent.Comp.SawOpen ? ent.Comp.OpenHitDelay : ent.Comp.ClosedHitDelay;
        _ai.SetSpeedMultiplier(ent, 0.5f);
        _ai.Schedule(ent, delay, () =>
        {
            _ai.SetSpeedMultiplier(ent, 1f);
            ChainAttack(ent, victim, sequenceHit + 1);
        });
    }

    /// <summary>kinetic_accelerator: тревога, стоит на месте, 3 выстрела через 0.15 с с разбросом 10°.</summary>
    private void FirePka(Entity<DeadMinerComponent> ent, EntityUid target, Action onDone)
    {
        ent.Comp.Busy = true;
        _ai.SetImmobile(ent, true);
        _popup.PopupEntity(Loc.GetString("dead-miner-fires"), ent, PopupType.MediumCaution);

        var wait = MathF.Max(0f, ent.Comp.PkaAlertDelay - ent.Comp.PkaPrefireDelay) + ent.Comp.PkaPrefireDelay;
        _ai.Schedule(ent, wait, () => PkaShot(ent, target, ent.Comp.PkaShots, () =>
        {
            ent.Comp.NextPka = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.PkaCooldown);
            _ai.Schedule(ent, ent.Comp.PkaReloadDelay, () =>
            {
                ent.Comp.Busy = false;
                _ai.SetImmobile(ent, false);
                onDone();
            });
        }));
    }

    private void PkaShot(Entity<DeadMinerComponent> ent, EntityUid target, int remaining, Action onDone)
    {
        if (remaining <= 0 || TerminatingOrDeleted(target))
        {
            onDone();
            return;
        }

        var angle = _ai.ByondAngle(Transform(ent).Coordinates, Transform(target).Coordinates)
                    + _random.NextFloat(-ent.Comp.PkaSpread, ent.Comp.PkaSpread);
        _ai.ShootProjectile(ent, ent.Comp.KineticProjectile, angle, ent.Comp.PkaSpeed);
        _ai.PlaySound(ent.Comp.KineticSound, Transform(ent).Coordinates);
        _ai.Schedule(ent, ent.Comp.PkaShotDelay, () => PkaShot(ent, target, remaining - 1, onDone));
    }

    /// <summary>
    /// dash_attack: рывок к цели (до 6 клеток, при столкновении — серия ударов),
    /// через 0.22 с — очередь из ПКА.
    /// </summary>
    private void DashAttack(Entity<DeadMinerComponent> ent, EntityUid target)
    {
        ent.Comp.NextDash = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.DashAttackCooldown);
        if (!_ai.TryGetTile(ent, out var grid, out var from) || !_ai.TryGetTile(target, out var targetGrid, out var targetTile) ||
            grid.Owner != targetGrid.Owner)
            return;

        ent.Comp.Busy = true;
        _ai.SetImmobile(ent, true);
        var end = targetTile + MegafaunaAiSystem.StepTowards(from, targetTile) * 2;
        _ai.Schedule(ent, ent.Comp.DashDelay, () => DashStep(ent, target, grid, end, ent.Comp.DashDistance));
        _ai.Schedule(ent, ent.Comp.DashFireDelay, () =>
        {
            _ai.SetImmobile(ent, false);
            ent.Comp.Busy = false;
            if (!TerminatingOrDeleted(target))
                FirePka(ent, target, () => { });
        });
    }

    private void DashStep(Entity<DeadMinerComponent> ent, EntityUid target, Entity<MapGridComponent> grid, Vector2i end, int remaining)
    {
        if (remaining <= 0 || !_ai.TryGetTile(ent, out _, out var current) || current == end)
            return;

        var next = current + MegafaunaAiSystem.StepTowards(current, end);
        if (_ai.IsBlocked(grid, next))
            return;

        if (_ai.MobsOnTile(grid, next).Contains(target))
        {
            // hit_target: врезался — сразу атакует.
            ent.Comp.NextMelee = _timing.CurTime;
            ChainAttack(ent, target, 1);
            return;
        }

        _ai.PlaySound(ent.Comp.DashSound, Transform(ent).Coordinates, -5f);
        _transform.SetCoordinates(ent, _ai.TileCenter(grid, next));
        _ai.Schedule(ent, ent.Comp.DashStepDelay, () => DashStep(ent, target, grid, end, remaining - 1));
    }
}
