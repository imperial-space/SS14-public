using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Gatherable.Components;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Shared.Chat;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.Colossus;

/// <summary>
/// Перенос megafauna/colossus из SS13: OpenFire, spiral_shots, random_aoe, shotgun_blast,
/// dir_shots/alternating, colossus_final, telegraph и projectile_shield.
/// </summary>
public sealed class ColossusSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly float[] Cardinals = { 0f, 90f, 180f, 270f };
    private static readonly float[] Diagonals = { 45f, 135f, 225f, 315f };
    private static readonly float[] AllDirs = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColossusComponent, MegafaunaOpenFireEvent>(OnOpenFire);
        SubscribeLocalEvent<ColossusComponent, ProjectileReflectAttemptEvent>(OnProjectile);
        SubscribeLocalEvent<ColossusComponent, HitScanReflectAttemptEvent>(OnHitscan);
        SubscribeLocalEvent<ColossusBoltComponent, ProjectileHitEvent>(OnBoltHit);
    }

    private void OnOpenFire(Entity<ColossusComponent> ent, ref MegafaunaOpenFireEvent args)
    {
        var target = args.Target;
        var anger = _ai.Anger(ent, 40f, 20f);

        if (_ai.GetHealth(ent) <= _ai.GetMaxHealth(ent) / 10f && ent.Comp.FinalAvailable)
        {
            if (TryFinal(ent, target))
                ent.Comp.FinalAvailable = false;
        }
        else if (_ai.Prob(20 + anger)) // Major attack
            TrySpiral(ent, target);
        else if (_ai.Prob(20))
            TryRandom(ent, target);
        else if (_ai.Prob(60 + anger))
            TryShotgun(ent, target);
        else
            TryDirShots(ent, target);
    }

    private void Say(EntityUid uid, string message)
    {
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, ChatTransmitRange.Normal, hideLog: true, ignoreActionBlocker: true);
    }

    /// <summary>telegraph: красная вспышка и тряска у всех в 10 клетках, рёв Нар'Си.</summary>
    private void Telegraph(Entity<ColossusComponent> ent)
    {
        var coords = Transform(ent).Coordinates;
        _ai.ShakeCamera(coords, 10f, 4f);
        _ai.PlaySound(ent.Comp.TelegraphSound, coords, 5f);
    }

    /// <summary>Способность колосса: фраза, затем через 1.5 с залп.</summary>
    private bool TryAbility(Entity<ColossusComponent> ent, string line, float windUp, Action<Action> sequence, float cooldown)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        Say(ent, line);
        _ai.Schedule(ent, windUp, () => sequence(() => _ai.EndAbility(ent, cooldown)));
        return true;
    }

    private bool TrySpiral(Entity<ColossusComponent> ent, EntityUid target)
    {
        if (!_ai.CanUseAbility(ent))
            return false;

        var enraged = _ai.GetHealth(ent) <= _ai.GetMaxHealth(ent) / 3f;
        Telegraph(ent);
        _appearance.SetData(ent, MegafaunaVisuals.State, "eva_attack");
        return TryAbility(ent, "Judgement.", ent.Comp.WindUp, done =>
        {
            void Finish()
            {
                _appearance.SetData(ent, MegafaunaVisuals.State, string.Empty);
                done();
            }

            if (enraged)
            {
                _ai.Schedule(ent, 1f, () =>
                {
                    Spiral(ent, true, 80, 8, () => { });
                    Spiral(ent, false, 80, 8, Finish);
                });
                return;
            }

            Spiral(ent, _random.Prob(0.5f), 80, 8, Finish);
        }, ent.Comp.SpiralCooldown);
    }

    /// <summary>create_spiral_attack: 80 болтов по кругу шагом 22.5°, раз в 0.1 с.</summary>
    private void Spiral(Entity<ColossusComponent> ent, bool negative, int remaining, int counter, Action done)
    {
        if (remaining <= 0)
        {
            done();
            return;
        }

        counter += negative ? -1 : 1;
        if (counter > 16)
            counter = 1;
        if (counter < 1)
            counter = 16;

        Shoot(ent, counter * 22.5f);
        _ai.PlaySound(ent.Comp.ShotSound, Transform(ent).Coordinates, -10f);
        _ai.Schedule(ent, 0.1f, () => Spiral(ent, negative, remaining - 1, counter, done));
    }

    private bool TryRandom(Entity<ColossusComponent> ent, EntityUid target)
    {
        return TryAbility(ent, "Wrath.", ent.Comp.WindUp, done =>
        {
            RandomShots(ent);
            done();
        }, ent.Comp.RandomCooldown);
    }

    /// <summary>random_aoe: 32 болта в случайные стороны.</summary>
    private void RandomShots(Entity<ColossusComponent> ent)
    {
        _ai.PlaySound(ent.Comp.ShotSound, Transform(ent).Coordinates, 8f);
        for (var i = 0; i < 32; i++)
            Shoot(ent, _random.NextFloat(0f, 360f));
    }

    private bool TryShotgun(Entity<ColossusComponent> ent, EntityUid target)
    {
        return TryAbility(ent, "Retribution.", ent.Comp.WindUp, done =>
        {
            Shotgun(ent, target);
            done();
        }, ent.Comp.ShotgunCooldown);
    }

    /// <summary>shotgun_blast: 6 болтов веером ±12.5° в цель.</summary>
    private void Shotgun(Entity<ColossusComponent> ent, EntityUid target)
    {
        if (TerminatingOrDeleted(target))
            return;

        _ai.PlaySound(ent.Comp.ShotSound, Transform(ent).Coordinates, 5f);
        var angle = _ai.ByondAngle(Transform(ent).Coordinates, Transform(target).Coordinates);
        foreach (var spread in ent.Comp.ShotgunAngles)
            Shoot(ent, angle + spread);
    }

    private bool TryDirShots(Entity<ColossusComponent> ent, EntityUid target)
    {
        return TryAbility(ent, "Lament.", ent.Comp.WindUp, done => Alternating(ent, done), ent.Comp.DirShotsCooldown);
    }

    /// <summary>dir_shots/alternating: диагонали, кресты, диагонали, кресты с паузой 1 с.</summary>
    private void Alternating(Entity<ColossusComponent> ent, Action done)
    {
        FireDirections(ent, Diagonals);
        _ai.Schedule(ent, 1f, () =>
        {
            FireDirections(ent, Cardinals);
            _ai.Schedule(ent, 1f, () =>
            {
                FireDirections(ent, Diagonals);
                _ai.Schedule(ent, 1f, () =>
                {
                    FireDirections(ent, Cardinals);
                    done();
                });
            });
        });
    }

    private void FireDirections(Entity<ColossusComponent> ent, float[] dirs)
    {
        _ai.PlaySound(ent.Comp.ShotSound, Transform(ent).Coordinates, 5f);
        foreach (var dir in dirs)
            Shoot(ent, dir);
    }

    /// <summary>colossus_final: «Perish.» — 20 волн дробовика и болтов по округе, затем 3 случайных залпа и 3 креста.</summary>
    private bool TryFinal(Entity<ColossusComponent> ent, EntityUid target)
    {
        if (!_ai.TryBeginAbility(ent))
            return false;

        Say(ent, "Perish.");
        _ai.Schedule(ent, 1.5f, () => FinalWave(ent, target, 20, 10));
        return true;
    }

    private void FinalWave(Entity<ColossusComponent> ent, EntityUid target, int remaining, int counter)
    {
        if (remaining <= 0)
        {
            FinalRandom(ent, target, 3, counter);
            return;
        }

        if (counter > 4)
        {
            Telegraph(ent);
            Shotgun(ent, target);
        }

        if (counter > 1)
            counter--;

        if (_ai.TryGetTile(ent, out var grid, out var origin))
        {
            var from = Transform(ent).Coordinates;
            foreach (var tile in MegafaunaAiSystem.RangeTiles(origin, 12))
            {
                if (tile == origin || !_ai.Prob(Math.Min(counter, 2)))
                    continue;
                Shoot(ent, _ai.ByondAngle(from, _ai.TileCenter(grid, tile)));
            }
        }

        var next = counter;
        _ai.Schedule(ent, (next + 1) / 10f, () => FinalWave(ent, target, remaining - 1, next));
    }

    private void FinalRandom(Entity<ColossusComponent> ent, EntityUid target, int remaining, int counter)
    {
        if (remaining <= 0)
        {
            FinalDirs(ent, 3);
            return;
        }

        Telegraph(ent);
        RandomShots(ent);
        counter += 6;
        _ai.Schedule(ent, counter / 10f, () => FinalRandom(ent, target, remaining - 1, counter));
    }

    private void FinalDirs(Entity<ColossusComponent> ent, int remaining)
    {
        if (remaining <= 0)
        {
            _ai.EndAbility(ent, ent.Comp.FinalCooldown);
            return;
        }

        Telegraph(ent);
        Alternating(ent, () => _ai.Schedule(ent, 1f, () => FinalDirs(ent, remaining - 1)));
    }

    private void Shoot(Entity<ColossusComponent> ent, float byondAngle)
    {
        _ai.ShootProjectile(ent, ent.Comp.Bolt, byondAngle, ent.Comp.BoltSpeed);
    }

    #region Projectile shield / bolts

    /// <summary>projectile_shield: при попадании пули вокруг колосса вспыхивает щит.</summary>
    private void OnProjectile(Entity<ColossusComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (!HasComp<ColossusBoltComponent>(args.ProjUid))
            SpawnShield(ent);
    }

    private void OnHitscan(Entity<ColossusComponent> ent, ref HitScanReflectAttemptEvent args)
    {
        SpawnShield(ent);
    }

    private void SpawnShield(Entity<ColossusComponent> ent)
    {
        if (!_mobState.IsAlive(ent))
            return;
        var offset = new System.Numerics.Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(0f, 2.25f));
        Spawn(ent.Comp.Shield, Transform(ent).Coordinates.Offset(offset));
    }

    /// <summary>projectile/colossus: мёртвых превращает в пепел, породу разрушает.</summary>
    private void OnBoltHit(Entity<ColossusBoltComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;
        if (HasComp<MobStateComponent>(target))
        {
            if (_mobState.IsDead(target) && !HasComp<MegafaunaAiComponent>(target))
            {
                Spawn("Ash", Transform(target).Coordinates);
                QueueDel(target);
            }

            return;
        }

        if (HasComp<GatherableComponent>(target) && _ai.TryGetTile(target, out var grid, out var tile))
            _ai.Drill(grid, tile);
    }

    #endregion
}
