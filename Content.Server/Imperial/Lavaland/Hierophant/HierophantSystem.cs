using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Hierophant;

/// <summary>
/// Перенос megafauna/hierophant из SS13: OpenFire, blink, blink_spam, cross_blast_spam, chaser_swarm,
/// chaser, blasts, burst, arena_trap, send_me_home и предсмертная вспышка.
/// </summary>
public sealed class HierophantSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private static readonly Color Purple = Color.FromHex("#660099");
    private static readonly Vector2i[] Cardinals = { Vector2i.Up, Vector2i.Down, Vector2i.Right, Vector2i.Left };
    private static readonly Vector2i[] Diagonals = { new(1, 1), new(-1, 1), new(1, -1), new(-1, -1) };
    private static readonly Vector2i[] AllDirs = Cardinals.Concat(Diagonals).ToArray();

    private static readonly string[] KillPhrases =
    {
        "Wsyvgi sj irivkc xettih. Vitemvmrk...", "Irivkc wsyvgi jsyrh. Vitemvmrk...",
        "Jyip jsyrh. Egxmzexmrk vitemv gcgpiw...", "Kix fiex. Liepmrk...",
    };

    private static readonly string[] TargetPhrases = { "Xevkix psgexih.", "Iriqc jsyrh.", "Eguymvih xevkix." };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HierophantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HierophantComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<HierophantComponent, MegafaunaOpenFireEvent>(OnOpenFire);
        SubscribeLocalEvent<HierophantComponent, MegafaunaTargetChangedEvent>(OnTargetChanged);
        SubscribeLocalEvent<HierophantComponent, MegafaunaDevourEvent>(OnDevour);
        SubscribeLocalEvent<HierophantComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HierophantComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<HierophantComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<HierophantComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<HierophantComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<HierophantWallComponent, PreventCollideEvent>(OnWallCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HierophantComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.GoHomeAt is not { } at || now < at)
                continue;
            comp.GoHomeAt = null;
            SendMeHome((uid, comp));
        }
    }

    #region Events

    private void OnMapInit(Entity<HierophantComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.SpawnedBeacon = Spawn(ent.Comp.Beacon, Transform(ent).Coordinates);
        _ai.SetImmobile(ent, true); // wander = FALSE: ждёт у маяка
    }

    private void OnShutdown(Entity<HierophantComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.SpawnedBeacon is { } beacon)
            QueueDel(beacon);
    }

    private void OnAttemptMelee(Entity<HierophantComponent> ent, ref AttemptMeleeEvent args)
    {
        if (ent.Comp.Blinking)
            args.Cancelled = true;
    }

    private void Say(EntityUid uid, string message)
    {
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, ChatTransmitRange.Normal, hideLog: true, ignoreActionBlocker: true);
    }

    /// <summary>GiveTarget: фраза о цели; если сидел у маяка — сразу арена вокруг себя.</summary>
    private void OnTargetChanged(Entity<HierophantComponent> ent, ref MegafaunaTargetChangedEvent args)
    {
        if (args.NewTarget != null)
        {
            ent.Comp.GoHomeAt = null;
            Say(ent, _random.Pick(TargetPhrases));
            _ai.SetImmobile(ent, ent.Comp.Blinking);
            if (ent.Comp.SpawnedBeacon is { } beacon && ent.Comp.SittingAtCenter &&
                _ai.TryGetTile(ent, out _, out var ours) && _ai.TryGetTile(beacon, out _, out var beaconTile) && ours == beaconTile)
            {
                ArenaTrap(ent, ent);
            }

            return;
        }

        if (ent.Comp.SpawnedBeacon != null && ent.Comp.GoHomeAt == null)
            ent.Comp.GoHomeAt = _timing.CurTime + TimeSpan.FromSeconds(30);
    }

    private void OnDevour(Entity<HierophantComponent> ent, ref MegafaunaDevourEvent args)
    {
        Say(ent, _random.Pick(KillPhrases));
        _popup.PopupEntity(Loc.GetString("hierophant-absorbs", ("boss", ent.Owner), ("victim", args.Victim)), ent, PopupType.LargeCaution);
    }

    private void OnDamageChanged(Entity<HierophantComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && !ent.Comp.Blinking)
        {
            ent.Comp.SittingAtCenter = false;
            _ai.SetImmobile(ent, false);
        }
    }

    /// <summary>Moved: квадраты за собой, грохот шагов, ловушка-арена для цели.</summary>
    private void OnMove(Entity<HierophantComponent> ent, ref MoveEvent args)
    {
        if (!_ai.TryGetTile(ent, out var grid, out var tile) || ent.Comp.LastTile == tile)
            return;

        if (ent.Comp.LastTile is { } old && !ent.Comp.Blinking && _mobState.IsAlive(ent))
        {
            SpawnSquares(ent, grid, old, tile - old);
            _ai.PlaySound(ent.Comp.MoveSound, _ai.TileCenter(grid, old));
            if (_ai.GetTarget(ent) is { } target)
                ArenaTrap(ent, target);
        }

        ent.Comp.LastTile = tile;
    }

    /// <summary>AttackingTarget: удар в ближнем бою вызывает вспышку и OpenFire.</summary>
    private void OnMeleeHit(Entity<HierophantComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || ent.Comp.Blinking || _ai.GetTarget(ent) is not { } target || !_mobState.IsAlive(target))
            return;

        var origin = Transform(ent).Coordinates;
        if (_timing.CurTime >= ent.Comp.RangedCooldown)
        {
            CalculateRage(ent);
            SetRangedCooldown(ent, ent.Comp.RangedCooldownTime);
            Burst(ent, origin, ent.Comp.BurstRange, 0.5f);
        }
        else
        {
            ent.Comp.BurstRange = 3;
            Burst(ent, origin, ent.Comp.BurstRange, 0.25f);
        }

        OpenFire(ent, target, true);
    }

    /// <summary>death: самоуничтожение — вспышка радиусом 10.</summary>
    private void OnMobStateChanged(Entity<HierophantComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        ent.Comp.Blinking = true;
        Say(ent, "Mrmxmexmrk wipj-hiwxvygx wiuyirgi...");
        _popup.PopupEntity(Loc.GetString("hierophant-shrinks", ("boss", ent.Owner)), ent, PopupType.LargeCaution);
        Burst(null, Transform(ent).Coordinates, 10, 0.5f, ent.Comp);
    }

    private void OnWallCollide(Entity<HierophantWallComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Caster is not { } caster)
            return;
        if (args.OtherEntity == caster ||
            TryComp<ProjectileComponent>(args.OtherEntity, out var projectile) && projectile.Shooter == caster)
            args.Cancelled = true;
    }

    #endregion

    #region OpenFire

    private void CalculateRage(Entity<HierophantComponent> ent)
    {
        ent.Comp.SittingAtCenter = false;
        ent.Comp.Anger = _ai.Anger(ent, 42f, 50f);
        ent.Comp.BurstRange = ent.Comp.BaseBurstRange + (int) MathF.Round(ent.Comp.Anger * 0.08f);
        ent.Comp.BeamRange = ent.Comp.BaseBeamRange + (int) MathF.Round(ent.Comp.Anger * 0.12f);
    }

    /// <summary>update_cooldowns(SET_RANGED = max(0.5 с, base - anger * 0.75 дс)).</summary>
    private void SetRangedCooldown(Entity<HierophantComponent> ent, float baseSeconds)
    {
        var seconds = MathF.Max(0.5f, baseSeconds - ent.Comp.Anger * 0.075f);
        ent.Comp.RangedCooldown = _timing.CurTime + TimeSpan.FromSeconds(seconds);
    }

    /// <summary>cached_multiplicative_slowdown цели в децисекундах на клетку.</summary>
    private float TargetSlowness(EntityUid target)
    {
        var slowness = 0f;
        if (TryComp<MovementSpeedModifierComponent>(target, out var move) && move.CurrentSprintSpeed > 0)
            slowness = 10f / move.CurrentSprintSpeed;
        return MathF.Max(slowness, 1f);
    }

    private void OnOpenFire(Entity<HierophantComponent> ent, ref MegafaunaOpenFireEvent args)
    {
        if (_timing.CurTime < ent.Comp.RangedCooldown)
            return;
        OpenFire(ent, args.Target, false);
    }

    private void OpenFire(Entity<HierophantComponent> ent, EntityUid target, bool fromMelee)
    {
        if (ent.Comp.Blinking)
            return;

        CalculateRage(ent);
        var anger = ent.Comp.Anger;
        var blinkCounter = 1 + (int) MathF.Round(anger * 0.08f);
        var crossCounter = 1 + (int) MathF.Round(anger * 0.12f);

        ArenaTrap(ent, target);
        SetRangedCooldown(ent, ent.Comp.RangedCooldownTime);

        var slowness = TargetSlowness(target);
        ent.Comp.ChaserSpeed = MathF.Max(1f, 3f - anger * 0.04f + (slowness - 1f) * 0.5f);

        var dist = _ai.TileDistance(ent, target);
        if (_mobState.IsDead(target) && dist > 2)
        {
            Blink(ent, target, null);
            return;
        }

        var chaserReady = _timing.CurTime >= ent.Comp.ChaserCooldown;
        if (_ai.Prob(anger * 0.75f)) // major ranged attack
        {
            var possibilities = new List<string>();
            if (crossCounter > 1)
                possibilities.Add("cross_blast_spam");
            if (dist > 2)
                possibilities.Add("blink_spam");
            if (chaserReady)
            {
                if (_ai.Prob(anger * 2))
                    possibilities = new List<string> { "chaser_swarm" };
                else
                    possibilities.Add("chaser_swarm");
            }

            if (possibilities.Count > 0)
            {
                switch (_random.Pick(possibilities))
                {
                    case "blink_spam":
                        BlinkSpam(ent, target, blinkCounter, slowness);
                        break;
                    case "cross_blast_spam":
                        CrossBlastSpam(ent, target, crossCounter, slowness);
                        break;
                    case "chaser_swarm":
                        ChaserSwarm(ent, target, slowness);
                        break;
                }

                return;
            }
        }

        if (chaserReady)
        {
            var first = SpawnChaser(ent, target, ent.Comp.ChaserSpeed, 0, null);
            ent.Comp.ChaserCooldown = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ChaserCooldownTime);
            if (_ai.Prob(anger) || dist <= 1)
            {
                var dirs = Cardinals.Where(d => d != first).ToList();
                SpawnChaser(ent, target, ent.Comp.ChaserSpeed * 1.5f, 4, _random.Pick(dirs));
            }
        }
        else if (_ai.Prob(10 + anger * 0.5f) && dist > 2)
        {
            Blink(ent, target, null);
        }
        else if (_ai.Prob(70 - anger)) // a cross blast of some type
        {
            if (_ai.Prob(anger * (2f / slowness)) && _ai.GetHealth(ent) < _ai.GetMaxHealth(ent) * 0.5f)
                Blasts(ent, target, AllDirs);
            else if (_ai.Prob(60))
                Blasts(ent, target, Cardinals);
            else
                Blasts(ent, target, Diagonals);
        }
        else // just release a burst of power
        {
            Burst(ent, Transform(ent).Coordinates, ent.Comp.BurstRange, 0.5f);
        }
    }

    private void StartMajor(Entity<HierophantComponent> ent, string phrase)
    {
        SetRangedCooldown(ent, ent.Comp.MajorAttackCooldown);
        Say(ent, phrase);
        ent.Comp.Blinking = true;
        _ai.SetImmobile(ent, true);
        _appearance.SetData(ent, MegafaunaVisuals.Color, Purple);
    }

    private void EndMajor(Entity<HierophantComponent> ent)
    {
        _appearance.SetData(ent, MegafaunaVisuals.Color, Color.White);
        _ai.Schedule(ent, 0.8f, () =>
        {
            ent.Comp.Blinking = false;
            _ai.SetImmobile(ent, ent.Comp.SittingAtCenter);
        });
    }

    /// <summary>blink_spam: ниже половины здоровья — серия телепортов за целью.</summary>
    private void BlinkSpam(Entity<HierophantComponent> ent, EntityUid target, int blinkCounter, float slowness)
    {
        if (!(_ai.GetHealth(ent) < _ai.GetMaxHealth(ent) * 0.5f && blinkCounter > 1))
        {
            SetRangedCooldown(ent, ent.Comp.MajorAttackCooldown);
            Blink(ent, target, null);
            return;
        }

        StartMajor(ent, "Mx ampp rsx iwgeti.");
        _ai.Schedule(ent, 0.6f, () => BlinkSpamStep(ent, target, blinkCounter, slowness));
    }

    private void BlinkSpamStep(Entity<HierophantComponent> ent, EntityUid target, int counter, float slowness)
    {
        if (TerminatingOrDeleted(target) || counter <= 0 ||
            _ai.TryGetTile(ent, out _, out var ours) && _ai.TryGetTile(target, out _, out var theirs) && ours == theirs)
        {
            EndMajor(ent);
            return;
        }

        ent.Comp.Blinking = false;
        Blink(ent, target, () =>
        {
            ent.Comp.Blinking = true;
            _ai.Schedule(ent, (4 + slowness) / 10f, () => BlinkSpamStep(ent, target, counter - 1, slowness));
        });
    }

    /// <summary>cross_blast_spam: несколько крестов под целью подряд.</summary>
    private void CrossBlastSpam(Entity<HierophantComponent> ent, EntityUid target, int crossCounter, float slowness)
    {
        StartMajor(ent, "Piezi mx rsalivi xs vyr.");
        _ai.Schedule(ent, 0.6f, () => CrossStep(ent, target, crossCounter, slowness));
    }

    private void CrossStep(Entity<HierophantComponent> ent, EntityUid target, int counter, float slowness)
    {
        if (TerminatingOrDeleted(target) || counter <= 0)
        {
            EndMajor(ent);
            return;
        }

        Blasts(ent, target, _ai.Prob(60) ? Cardinals : Diagonals);
        _ai.Schedule(ent, (6 + slowness) / 10f, () => CrossStep(ent, target, counter - 1, slowness));
    }

    /// <summary>chaser_swarm: до четырёх преследователей по разным направлениям.</summary>
    private void ChaserSwarm(Entity<HierophantComponent> ent, EntityUid target, float slowness)
    {
        StartMajor(ent, "Mx gerrsx lmhi.");
        _ai.Schedule(ent, 0.6f, () =>
        {
            var targets = new List<EntityUid>();
            if (TryComp<MegafaunaAiComponent>(ent, out var ai))
            {
                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, ai.AggroRange))
                {
                    if (mob.Owner != ent.Owner && !HasComp<MegafaunaAiComponent>(mob) && _mobState.IsAlive(mob))
                        targets.Add(mob);
                }
            }

            SwarmStep(ent, target, targets, Cardinals.ToList(), slowness);
        });
    }

    private void SwarmStep(Entity<HierophantComponent> ent, EntityUid mainTarget, List<EntityUid> targets, List<Vector2i> dirs, float slowness)
    {
        if (targets.Count == 0 || dirs.Count == 0)
        {
            ent.Comp.ChaserCooldown = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ChaserCooldownTime);
            EndMajor(ent);
            return;
        }

        var picked = targets.Count >= dirs.Count ? _random.PickAndTake(targets) : _random.Pick(targets);
        if (!_mobState.IsAlive(picked))
        {
            picked = mainTarget;
            if (TerminatingOrDeleted(picked) || !_mobState.IsAlive(picked))
            {
                ent.Comp.ChaserCooldown = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ChaserCooldownTime);
                EndMajor(ent);
                return;
            }
        }

        SpawnChaser(ent, picked, ent.Comp.ChaserSpeed, 3, _random.PickAndTake(dirs));
        _ai.Schedule(ent, (8 + slowness) / 10f, () => SwarmStep(ent, mainTarget, targets, dirs, slowness));
    }

    #endregion

    #region Attacks

    /// <summary>blasts: телеграф под целью, через 0.2 с крест взрывов длиной beam_range.</summary>
    private void Blasts(Entity<HierophantComponent> ent, EntityUid victim, Vector2i[] dirs)
    {
        if (!_ai.TryGetTile(victim, out var grid, out var tile))
            return;

        var telegraph = dirs == Cardinals ? ent.Comp.TelegraphCardinal
            : dirs == Diagonals ? ent.Comp.TelegraphDiagonal
            : ent.Comp.Telegraph;
        _ai.SpawnAt(telegraph, grid, tile);
        _ai.PlaySound(ent.Comp.TelegraphSound, _ai.TileCenter(grid, tile));

        _ai.Schedule(ent, 0.2f, () =>
        {
            SpawnBlast(ent, ent.Comp, grid, tile, ent.Comp.BlastDamage);
            foreach (var dir in dirs)
            {
                for (var i = 1; i <= ent.Comp.BeamRange; i++)
                    SpawnBlast(ent, ent.Comp, grid, tile + dir * i, ent.Comp.BlastDamage);
            }
        });
    }

    /// <summary>blink: телеграф на обеих клетках, взрывы 3x3 по 30, исчезает и появляется у цели.</summary>
    private void Blink(Entity<HierophantComponent> ent, EntityUid victim, Action? onDone)
    {
        if (ent.Comp.Blinking ||
            !_ai.TryGetTile(victim, out var grid, out var target) ||
            !_ai.TryGetTile(ent, out var ownGrid, out var source) ||
            grid.Owner != ownGrid.Owner)
        {
            onDone?.Invoke();
            return;
        }

        BlinkTo(ent, grid, target, source, onDone);
    }

    private void BlinkTo(Entity<HierophantComponent> ent, Entity<MapGridComponent> grid, Vector2i target, Vector2i source, Action? onDone)
    {
        var uid = ent.Owner;
        _ai.SpawnAt(ent.Comp.Telegraph, grid, target);
        _ai.SpawnAt(ent.Comp.Telegraph, grid, source);
        _ai.PlaySound(ent.Comp.TeleportSound, _ai.TileCenter(grid, target));
        _ai.PlaySound(ent.Comp.BurstSound, _ai.TileCenter(grid, source));
        ent.Comp.Blinking = true;
        _ai.SetImmobile(uid, true);

        _ai.Schedule(uid, 0.2f, () =>
        {
            _ai.SpawnAt(ent.Comp.TelegraphTeleport, grid, target);
            _ai.SpawnAt(ent.Comp.TelegraphTeleport, grid, source);
            foreach (var t in MegafaunaAiSystem.RangeTiles(target, 1))
                SpawnBlast(uid, ent.Comp, grid, t, ent.Comp.BlinkBlastDamage);
            foreach (var t in MegafaunaAiSystem.RangeTiles(source, 1))
                SpawnBlast(uid, ent.Comp, grid, t, ent.Comp.BlinkBlastDamage);

            _ai.Schedule(uid, 0.1f, () =>
            {
                _popup.PopupEntity(Loc.GetString("hierophant-fades-out", ("boss", uid)), uid, PopupType.MediumCaution);
                _appearance.SetData(uid, MegafaunaVisuals.Color, Color.White.WithAlpha(0f));
                _physics.SetCanCollide(uid, false);
                _ai.Schedule(uid, 0.2f, () =>
                {
                    _transform.SetCoordinates(uid, _ai.TileCenter(grid, target));
                    _ai.Schedule(uid, 0.1f, () =>
                    {
                        _appearance.SetData(uid, MegafaunaVisuals.Color, Color.White);
                        _ai.Schedule(uid, 0.1f, () =>
                        {
                            _physics.SetCanCollide(uid, true);
                            _popup.PopupEntity(Loc.GetString("hierophant-fades-in", ("boss", uid)), uid, PopupType.MediumCaution);
                            _ai.Schedule(uid, 0.1f, () =>
                            {
                                ent.Comp.Blinking = false;
                                _ai.SetImmobile(uid, ent.Comp.SittingAtCenter);
                                onDone?.Invoke();
                            });
                        });
                    });
                });
            });
        });
    }

    /// <summary>hierophant_burst: расходящийся квадрат взрывов, дальние кольца быстрее.</summary>
    private void Burst(EntityUid? caster, Robust.Shared.Map.EntityCoordinates origin, int range, float spreadSpeed, HierophantComponent? comp = null)
    {
        if (caster != null && !Resolve(caster.Value, ref comp))
            return;
        if (comp == null || !_ai.TryGetTile(origin, out var grid, out var center))
            return;

        _ai.PlaySound(comp.BurstSound, origin, 10f);
        var delay = 0f;
        for (var dist = 0; dist <= range; dist++)
        {
            if (dist > 0)
                delay += (1 + Math.Min(range - dist, 12) * spreadSpeed) / 10f;

            var ring = dist;
            _ai.Schedule(null, delay, () =>
            {
                foreach (var tile in MegafaunaAiSystem.RangeTiles(center, ring))
                {
                    if (MegafaunaAiSystem.Chebyshev(tile, center) == ring)
                        SpawnBlast(caster, comp, grid, tile, comp.BlastDamage);
                }
            });
        }
    }

    /// <summary>arena_trap: стена радиусом 11 вокруг цели на 10 с; если далеко — телепорт.</summary>
    private void ArenaTrap(Entity<HierophantComponent> ent, EntityUid victim)
    {
        if (_timing.CurTime < ent.Comp.ArenaCooldown || !_mobState.IsAlive(victim) ||
            !_ai.TryGetTile(victim, out var grid, out var center))
            return;

        if (victim != ent.Owner && (InArena(ent, victim) || InArena(ent, ent)))
            return;

        ent.Comp.ArenaCooldown = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ArenaCooldownTime);
        foreach (var dir in Cardinals)
            ArenaSquares(ent, grid, center, dir, 1);

        foreach (var tile in MegafaunaAiSystem.RangeTiles(center, 11))
        {
            if (MegafaunaAiSystem.Chebyshev(tile, center) != 11)
                continue;
            var wall = _ai.SpawnAt(ent.Comp.Wall, grid, tile);
            EnsureComp<HierophantWallComponent>(wall).Caster = ent;
            SpawnBlast(ent, ent.Comp, grid, tile, ent.Comp.BlastDamage);
        }

        if (_ai.TileDistance(ent, victim) >= 11 && _ai.TryGetTile(ent, out _, out var source))
            BlinkTo(ent, grid, center, source, null);
    }

    /// <summary>Цель внутри родной арены иерофанта (area/ruin/unpowered/hierophant).</summary>
    private bool InArena(Entity<HierophantComponent> ent, EntityUid uid)
    {
        if (ent.Comp.SpawnedBeacon is not { } beacon ||
            !_ai.TryGetTile(beacon, out var beaconGrid, out var beaconTile) ||
            !_ai.TryGetTile(uid, out var grid, out var tile) ||
            grid.Owner != beaconGrid.Owner)
            return false;

        return !HasComp<MapComponent>(grid) || MegafaunaAiSystem.Chebyshev(tile, beaconTile) <= 10;
    }

    private void ArenaSquares(Entity<HierophantComponent> ent, Entity<MapGridComponent> grid, Vector2i from, Vector2i dir, int step)
    {
        if (step > 10)
            return;
        SpawnSquares(ent, grid, from + dir * step, dir);
        _ai.Schedule(ent, 0.05f, () => ArenaSquares(ent, grid, from, dir, step + 1));
    }

    private void SpawnSquares(Entity<HierophantComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, Vector2i dir)
    {
        _ai.Drill(grid, tile, ent);
        var squares = _ai.SpawnAt(ent.Comp.Squares, grid, tile);
        if (dir != Vector2i.Zero)
            _transform.SetLocalRotation(squares, new Angle(new System.Numerics.Vector2(dir.X, dir.Y)) - Angle.FromDegrees(90));
    }

    #endregion

    #region Chaser

    private sealed class Chaser
    {
        public EntityUid? Caster;
        public HierophantComponent Comp = default!;
        public EntityUid Target;
        public Entity<MapGridComponent> Grid;
        public Vector2i Pos;
        public Vector2i MovingDir;
        public Vector2i PreviousDir;
        public Vector2i MorePreviousDir;
        public int Moving;
        public float Speed;
        public TimeSpan End;
    }

    /// <summary>temp_visual/hierophant/chaser: идёт к цели по сторонам света, оставляя взрывы.</summary>
    private Vector2i SpawnChaser(Entity<HierophantComponent> ent, EntityUid target, float speed, int moving, Vector2i? movingDir)
    {
        if (!_ai.TryGetTile(ent, out var grid, out var pos))
            return Vector2i.Zero;

        var chaser = new Chaser
        {
            Caster = ent,
            Comp = ent.Comp,
            Target = target,
            Grid = grid,
            Pos = pos,
            Moving = moving,
            MovingDir = movingDir ?? Vector2i.Zero,
            Speed = speed,
            End = _timing.CurTime + TimeSpan.FromSeconds(9.8),
        };
        _ai.Schedule(null, 0.1f, () => ChaserStep(chaser));
        return chaser.MovingDir;
    }

    /// <summary>get_cardinal_dir: случайно по оси, взвешенно по расстоянию.</summary>
    private Vector2i CardinalDir(Vector2i from, Vector2i to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = Math.Abs(to.Y - from.Y);
        if (dx == 0 && dy == 0)
            return Vector2i.Zero;
        return _random.NextFloat() * (dx + dy) < dy
            ? new Vector2i(0, Math.Sign(to.Y - from.Y))
            : new Vector2i(Math.Sign(to.X - from.X), 0);
    }

    private void ChaserStep(Chaser c)
    {
        if (_timing.CurTime >= c.End || TerminatingOrDeleted(c.Target) || !_ai.TryGetTile(c.Target, out var targetGrid, out var targetTile) ||
            targetGrid.Owner != c.Grid.Owner)
            return;

        if (c.Moving <= 0)
        {
            c.MorePreviousDir = c.PreviousDir;
            c.PreviousDir = c.MovingDir;
            var dir = CardinalDir(c.Pos, targetTile);
            if (dir != c.PreviousDir && dir == c.MorePreviousDir || dir == Vector2i.Zero)
                dir = _random.Pick(Cardinals.Where(d => d != c.MorePreviousDir).ToList());
            c.MovingDir = dir;

            var standard = CardinalDir(c.Pos, targetTile);
            c.Moving = standard != c.PreviousDir && standard == c.MorePreviousDir || standard == Vector2i.Zero ? 1 : 4;
        }

        c.Pos += c.MovingDir;
        SpawnBlast(c.Caster, c.Comp, c.Grid, c.Pos, c.Comp.BlastDamage);
        c.Moving--;
        _ai.Schedule(null, c.Speed / 10f, () => ChaserStep(c));
    }

    #endregion

    #region Blast

    /// <summary>
    /// blast/damaging: дробит породу, через 0.6 с бьёт всех на клетке (10 ожога, мегафауне и
    /// местной фауне — вдвое больше).
    /// </summary>
    private void SpawnBlast(EntityUid? caster, HierophantComponent comp, Entity<MapGridComponent> grid, Vector2i tile, float damage)
    {
        _ai.Drill(grid, tile, caster);
        _ai.SpawnAt(comp.Blast, grid, tile);
        _ai.PlaySound(comp.BlastSound, _ai.TileCenter(grid, tile), -5f);

        var hit = new HashSet<EntityUid>();
        if (caster != null)
            hit.Add(caster.Value);

        _ai.Schedule(null, 0.6f, () =>
        {
            BlastDamage(caster, comp, grid, tile, damage, hit);
            // bursting: ещё 0.13 с бьёт вошедших на клетку.
            _ai.Schedule(null, 0.13f, () => BlastDamage(caster, comp, grid, tile, damage, hit));
        });
    }

    private void BlastDamage(EntityUid? caster, HierophantComponent comp, Entity<MapGridComponent> grid, Vector2i tile, float damage, HashSet<EntityUid> hit)
    {
        foreach (var mob in _ai.MobsOnTile(grid, tile))
        {
            if (!hit.Add(mob) || _mobState.IsDead(mob))
                continue;

            _ai.PlaySound(comp.HitSound, Transform(mob).Coordinates, -5f);
            _popup.PopupEntity(Loc.GetString("hierophant-blast-hit"), mob, mob, PopupType.LargeCaution);
            _ai.Damage(mob, "Heat", damage, caster);
            if (HasComp<MegafaunaAiComponent>(mob) || HasComp<Content.Server.Imperial.Lavaland.MegafaunaTracker.LavalandMegafaunaComponent>(mob))
                _ai.Damage(mob, "Blunt", damage, caster);
        }
    }

    #endregion

    /// <summary>send_me_home: через 30 с без цели возвращается к маяку и чинится.</summary>
    private void SendMeHome(Entity<HierophantComponent> ent)
    {
        if (ent.Comp.SpawnedBeacon is not { } beacon || TerminatingOrDeleted(beacon) || !_mobState.IsAlive(ent))
            return;

        ent.Comp.SittingAtCenter = true;
        _popup.PopupEntity("\"Vixyvrmrk xs fewi...\"", ent, PopupType.Large);
        Blink(ent, beacon, null);

        var missing = _ai.GetMaxHealth(ent) - _ai.GetHealth(ent);
        _ai.Heal(ent, MathF.Max(missing * 0.5f, 250f));
        _popup.PopupEntity(_ai.GetHealth(ent) > _ai.GetMaxHealth(ent) * 0.9f
            ? "\"Vitemvw gsqtpixi. Stivexmrk ex qebmqyq ijjmgmirgc.\""
            : "\"Vitemvw gsqtpixi. Stivexmsrep ijjmgmirgc gsqtvsqmwih.\"", ent, PopupType.Large);
    }
}
