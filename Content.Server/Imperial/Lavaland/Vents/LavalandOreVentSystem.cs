using System.Linq;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.Tiles;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Imperial.Lavaland.Vents;
using Content.Shared.Interaction;
using Content.Shared.Mining.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Vents;

/// <summary>
/// Перенос structure/ore_vent, mob/node_drone и item/boulder из SS13: сканирование,
/// оборона дрона, добыча валунов, разбивка киркой и плоты на лаве.
/// </summary>
public sealed class LavalandOreVentSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly LavalandGeyserSystem _geyser = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly Content.Server.Chat.Systems.ChatSystem _chat = default!;

    private static readonly ProtoId<TagPrototype> PickaxeTag = "Pickaxe";
    private const int MineralsPerBoulder = 3;
    private const int MineralTypeOptions = 4;
    private const int MinerPointMultiplier = 100;
    private static readonly Robust.Shared.Audio.SoundSpecifier _platformSinkSound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Imperial/Lavaland/gas_hissing.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandOreVentComponent, MapInitEvent>(OnVentMapInit);
        SubscribeLocalEvent<LavalandOreVentComponent, InteractUsingEvent>(OnVentInteractUsing);
        SubscribeLocalEvent<LavalandOreVentComponent, OreVentScanDoAfterEvent>(OnVentScanned);
        SubscribeLocalEvent<LavalandOreVentComponent, ExaminedEvent>(OnVentExamined);

        SubscribeLocalEvent<LavalandNodeDroneComponent, MobStateChangedEvent>(OnDroneStateChanged);

        SubscribeLocalEvent<LavalandBoulderComponent, InteractUsingEvent>(OnBoulderInteractUsing);
        SubscribeLocalEvent<LavalandBoulderComponent, BoulderBreakDoAfterEvent>(OnBoulderBreak);
        SubscribeLocalEvent<LavalandBoulderComponent, AfterInteractEvent>(OnBoulderAfterInteract);
        SubscribeLocalEvent<LavalandBoulderComponent, LandEvent>(OnBoulderLand);
        SubscribeLocalEvent<LavalandBoulderComponent, ExaminedEvent>(OnBoulderExamined);
    }

    private static float WaveDuration(OreVentSize size) => size switch
    {
        OreVentSize.Large => 150f,
        OreVentSize.Medium => 90f,
        _ => 60f,
    };

    private static float PlatformLife(OreVentSize size) => size switch
    {
        OreVentSize.Large => 90f,
        OreVentSize.Medium => 45f,
        _ => 20f,
    };

    private static string SizeName(OreVentSize size) => size switch
    {
        OreVentSize.Large => "large",
        OreVentSize.Medium => "medium",
        _ => "small",
    };

    #region Vent

    private void OnVentMapInit(Entity<LavalandOreVentComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;

        // vent_size_setup(random = TRUE)
        if (comp.FixedSize is { } fixedSize)
            comp.Size = fixedSize;
        else
        {
            var roll = _random.NextFloat() * comp.SizeWeights.Values.Sum();
            foreach (var (size, weight) in comp.SizeWeights)
            {
                roll -= weight;
                if (roll > 0)
                    continue;
                comp.Size = size;
                break;
            }

            _meta.SetEntityName(ent, Loc.GetString("lavaland-ore-vent-name-" + SizeName(comp.Size)));
        }

        // generate_mineral_breakdown: 4 разных минерала, каждому вес rand(1, 4)
        if (comp.Minerals.Count == 0)
        {
            var pool = new Dictionary<EntProtoId, float>(comp.LavalandMinerals);
            for (var i = 0; i < MineralTypeOptions && pool.Count > 0; i++)
            {
                var picked = PickWeighted(pool);
                pool.Remove(picked);
                comp.Minerals[picked] = _random.Next(1, MineralTypeOptions + 1);
            }
        }

        comp.BoulderStyle = _random.Pick(new[] { "boulder", "rock", "stone" });
        if (!comp.Discovered && !comp.Tapped)
            EnsureComp<MiningShop.LavalandUndiscoveredVentComponent>(ent);
        if (comp.Bosses.Count > 0)
            comp.SummonedBoss = _random.Pick(comp.Bosses);

        UpdateVentVisuals(ent);
    }

    private EntProtoId PickWeighted(Dictionary<EntProtoId, float> weights)
    {
        var roll = _random.NextFloat() * weights.Values.Sum();
        foreach (var (key, weight) in weights)
        {
            roll -= weight;
            if (roll <= 0)
                return key;
        }

        return weights.Keys.Last();
    }

    private void UpdateVentVisuals(Entity<LavalandOreVentComponent> ent)
    {
        _appearance.SetData(ent, MegafaunaVisuals.State, ent.Comp.Tapped ? "ore_vent_active" : "ore_vent");
        _appearance.SetData(ent, MegafaunaVisuals.Overlay, ent.Comp.Tapped ? "well" : string.Empty);
    }

    private void OnVentExamined(Entity<LavalandOreVentComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Discovered)
        {
            var ores = string.Join(", ", ent.Comp.Minerals.Keys.Select(o => _proto.Index<EntityPrototype>(o).Name));
            args.PushMarkup(Loc.GetString("lavaland-ore-vent-examine-" + SizeName(ent.Comp.Size), ("ores", ores)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("lavaland-ore-vent-examine-scan"));
        }

        if (ent.Comp.ArtifactChance > 0)
            args.PushMarkup(Loc.GetString("lavaland-ore-vent-examine-artifact"));

        if (ent.Comp.SummonedBoss is { } boss)
            args.PushMarkup(Loc.GetString("lavaland-ore-vent-examine-boss-" + boss.Id));
    }

    /// <summary>scan_and_confirm.</summary>
    private void OnVentInteractUsing(Entity<LavalandOreVentComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<MiningScannerComponent>(args.Used))
            return;
        args.Handled = true;

        var comp = ent.Comp;
        if (comp.Tapped)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-ore-vent-tapped"), ent);
            return;
        }

        if (comp.WaveActive || comp.Node != null)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-ore-vent-protect"), ent);
            return;
        }

        if (!comp.Discovered)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-ore-vent-scanning"), ent, args.User);
            _ai.PlaySound(comp.ScanSound, Transform(ent).Coordinates, -5f);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, 4f, new OreVentScanDoAfterEvent(), ent, ent, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        // tgui_alert «Begin defending ore vent?» — подтверждение повторным сканированием.
        if (_timing.CurTime > comp.ConfirmUntil)
        {
            comp.ConfirmUntil = _timing.CurTime + TimeSpan.FromSeconds(10);
            _popup.PopupEntity(Loc.GetString(comp.Bosses.Count > 0
                ? "lavaland-ore-vent-confirm-boss"
                : "lavaland-ore-vent-confirm"), ent, args.User, PopupType.LargeCaution);
            return;
        }

        comp.ConfirmUntil = TimeSpan.Zero;
        PreWaveDefense(ent);
    }

    private void OnVentScanned(Entity<LavalandOreVentComponent> ent, ref OreVentScanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Discovered)
            return;
        args.Handled = true;

        ent.Comp.Discovered = true;
        _popup.PopupEntity(Loc.GetString("lavaland-ore-vent-scanned"), ent, args.User);
        EnsureComp<Gps.ImperialGpsComponent>(ent).Tag = MetaData(ent).EntityName;
        RemComp<MiningShop.LavalandUndiscoveredVentComponent>(ent);
        _geyser.AwardPoints(args.User, MinerPointMultiplier);
    }

    /// <summary>pre_wave_defense: дрон садится на жилу, округа расчищается, на лаве — плоты.</summary>
    private void PreWaveDefense(Entity<LavalandOreVentComponent> ent)
    {
        var comp = ent.Comp;
        comp.WaveActive = true;

        if (comp.Bosses.Count == 0)
            SpawnNode(ent);

        if (!_ai.TryGetTile(ent, out var grid, out var center))
            return;

        var duration = WaveDuration(comp.Size);
        for (var i = 1; i <= 5; i++)
        {
            var ring = i;
            _ai.Schedule(null, (ring - 1) * 0.6f, () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;
                foreach (var tile in MegafaunaAiSystem.RangeTiles(center, ring))
                {
                    if (tile == center)
                        continue;

                    if (_ai.IsMineral(grid, tile))
                    {
                        if (!_random.Prob((50 + ring * 8) / 100f))
                            _ai.Drill(grid, tile);
                        continue;
                    }

                    if (HasLava(grid, tile) && !HasPlatform(grid, tile) && !_random.Prob((30 + ring * 8) / 100f))
                        SpawnPlatform(comp.Platform, grid, tile, duration);
                }
            });
        }

        _ai.Schedule(null, 3f, () =>
        {
            if (!TerminatingOrDeleted(ent))
                StartWaveDefense(ent);
        });
    }

    private void SpawnNode(Entity<LavalandOreVentComponent> ent)
    {
        var node = Spawn(ent.Comp.NodeDrone, Transform(ent).Coordinates);
        var drone = EnsureComp<LavalandNodeDroneComponent>(node);
        drone.Vent = ent;
        ent.Comp.Node = node;

        // arrive: maxHealth = 300 + (boulder_size / 5) * 100
        var health = 300 + (int) ent.Comp.Size / 5 * 100;
        _thresholds.SetMobStateThreshold(node, health, MobState.Dead);
        _appearance.SetData(node, MegafaunaVisuals.State, "mining_node_flying");
        _ai.Schedule(null, 2f, () =>
        {
            if (!TerminatingOrDeleted(node))
                _appearance.SetData(node, MegafaunaVisuals.State, string.Empty);
        });
    }

    /// <summary>start_wave_defense.</summary>
    private void StartWaveDefense(Entity<LavalandOreVentComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.SummonedBoss is { } bossProto)
        {
            // boss/start_wave_defense: вместо волн — один босс.
            comp.Boss = Spawn(bossProto, Transform(ent).Coordinates);
            _popup.PopupEntity(Loc.GetString("lavaland-ore-vent-boss-emerges", ("boss", comp.Boss.Value)), ent, PopupType.LargeCaution);
            return;
        }

        comp.WaveEnd = _timing.CurTime + TimeSpan.FromSeconds(WaveDuration(comp.Size));
        comp.NextSpawn = _timing.CurTime + TimeSpan.FromSeconds(SpawnTime(comp));
    }

    /// <summary>spawn_time = 10 с + 5 с * (boulder_size / 5).</summary>
    private static float SpawnTime(LavalandOreVentComponent comp) => 10f + 5f * ((int) comp.Size / 5f);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<LavalandOreVentComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);

            if (comp.Tapped)
            {
                if (now >= comp.NextBoulder)
                {
                    comp.NextBoulder = now + TimeSpan.FromSeconds(comp.BoulderInterval);
                    if (CountBoulders(uid) < comp.MaxBoulders)
                        ProduceBoulder(ent);
                }

                continue;
            }

            if (!comp.WaveActive)
                continue;

            // Босс-жила: после смерти босса прилетает дрон, и жила вскрыта.
            if (comp.Boss is { } boss)
            {
                if (TerminatingOrDeleted(boss) || _mobState.IsDead(boss))
                {
                    comp.Boss = null;
                    SpawnNode(ent);
                    WaveWin(ent);
                }

                continue;
            }

            if (comp.WaveEnd == TimeSpan.Zero)
                continue;

            // handle_wave_conclusion: дрон погиб или его сдвинули — провал.
            if (comp.Node is not { } node || TerminatingOrDeleted(node) || _mobState.IsDead(node))
            {
                WaveLoss(ent, "lavaland-ore-vent-loss-destroyed");
                continue;
            }

            if (!_ai.TryGetTile(node, out _, out var nodeTile) || !_ai.TryGetTile(uid, out _, out var ventTile) || nodeTile != ventTile)
            {
                WaveLoss(ent, "lavaland-ore-vent-loss-detached");
                continue;
            }

            UpdateNodeProgress(ent, node);

            if (now >= comp.WaveEnd)
            {
                WaveWin(ent);
                continue;
            }

            if (now >= comp.NextSpawn)
            {
                comp.NextSpawn = now + TimeSpan.FromSeconds(SpawnTime(comp));
                SpawnDefenders(ent);
            }
        }
    }

    private void UpdateNodeProgress(Entity<LavalandOreVentComponent> ent, EntityUid node)
    {
        var total = WaveDuration(ent.Comp.Size);
        var fraction = (float) (ent.Comp.WaveEnd - _timing.CurTime).TotalSeconds / total;
        var state = fraction <= 0.3f ? "node_progress_4"
            : fraction <= 0.55f ? "node_progress_3"
            : fraction <= 0.8f ? "node_progress_2"
            : "node_progress_1";
        _appearance.SetData(node, MegafaunaVisuals.Overlay, state);
    }

    /// <summary>component/spawner: до 10 мобов, по 1 + size/5 за раз, в 4 клетках от жилы.</summary>
    private void SpawnDefenders(Entity<LavalandOreVentComponent> ent)
    {
        var comp = ent.Comp;
        comp.Spawned.RemoveAll(m => TerminatingOrDeleted(m) || _mobState.IsDead(m));
        var budget = Math.Min(1 + (int) comp.Size / 5, 10 - comp.Spawned.Count);
        if (budget <= 0 || comp.DefendingMobs.Count == 0 || !_ai.TryGetTile(ent, out var grid, out var center))
            return;

        var free = MegafaunaAiSystem.RangeTiles(center, 4)
            .Where(t => MegafaunaAiSystem.Chebyshev(t, center) == 4 && !_ai.IsBlocked(grid, t) && !HasLava(grid, t))
            .ToList();

        if (free.Count == 0)
        {
            // anti_cheese: жилу замуровали — взрыв расчищает округу.
            _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "Default", 30f, 5f, 10f, ent);
            return;
        }

        for (var i = 0; i < budget && free.Count > 0; i++)
        {
            var mob = _ai.SpawnAt(_random.Pick(comp.DefendingMobs), grid, _random.PickAndTake(free));
            comp.Spawned.Add(mob);
            _popup.PopupEntity(Loc.GetString("lavaland-ore-vent-mob-emerges", ("mob", mob), ("vent", ent.Owner)), mob, PopupType.MediumCaution);
        }
    }

    private void WaveLoss(Entity<LavalandOreVentComponent> ent, string message)
    {
        _popup.PopupEntity(Loc.GetString(message, ("vent", ent.Owner)), ent, PopupType.LargeCaution);
        ent.Comp.WaveActive = false;
        ent.Comp.WaveEnd = TimeSpan.Zero;
        ResetDrone(ent, false, ent.Comp.DroneCrashSound);
    }

    /// <summary>initiate_wave_win: жила вскрыта, награда всем шахтёрам в 7 клетках.</summary>
    private void WaveWin(Entity<LavalandOreVentComponent> ent)
    {
        var comp = ent.Comp;
        comp.WaveActive = false;
        comp.WaveEnd = TimeSpan.Zero;
        comp.Tapped = true;
        RemComp<Gps.ImperialGpsComponent>(ent);
        RemComp<MiningShop.LavalandUndiscoveredVentComponent>(ent);
        comp.NextBoulder = _timing.CurTime + TimeSpan.FromSeconds(comp.BoulderInterval);
        UpdateVentVisuals(ent);
        _popup.PopupEntity(Loc.GetString("lavaland-ore-vent-tapped"), ent, PopupType.Large);
        ResetDrone(ent, true, comp.DroneCrashSound);

        var reward = MinerPointMultiplier * ((int) comp.Size + 2) - MinerPointMultiplier;
        foreach (var miner in _lookup.GetEntitiesInRange<ActorComponent>(Transform(ent).Coordinates, 7f))
        {
            if (_mobState.IsAlive(miner))
                _geyser.AwardPoints(miner, reward);
        }
    }

    /// <summary>reset_drone + node_drone/escape.</summary>
    private void ResetDrone(Entity<LavalandOreVentComponent> ent, bool success, Robust.Shared.Audio.SoundSpecifier crashSound)
    {
        if (ent.Comp.Node is not { } node)
            return;
        ent.Comp.Node = null;

        if (TerminatingOrDeleted(node) || !TryComp<LavalandNodeDroneComponent>(node, out var drone) || drone.Escaping)
            return;

        drone.Escaping = true;
        drone.Vent = null;
        _appearance.SetData(node, MegafaunaVisuals.Overlay, string.Empty);
        _appearance.SetData(node, MegafaunaVisuals.State, "mining_node_escape");
        _ai.Schedule(null, 1.9f, () =>
        {
            if (TerminatingOrDeleted(node))
                return;
            _appearance.SetData(node, MegafaunaVisuals.State, "mining_node_flying");
            // prob(1): дрон не долетел до родной планеты.
            var funny = _random.Prob(0.01f);
            if (funny)
                _chat.TrySendInGameICMessage(node, Loc.GetString("lavaland-node-funny"), InGameICChatType.Speak, ChatTransmitRange.Normal, hideLog: true, ignoreActionBlocker: true);
            _popup.PopupEntity(Loc.GetString(success ? "lavaland-node-escape-success" : "lavaland-node-escape-fail"), node, PopupType.Medium);
            _ai.Schedule(null, 2f, () =>
            {
                if (funny && !TerminatingOrDeleted(node))
                {
                    _ai.PlaySound(crashSound, Transform(node).Coordinates);
                    _popup.PopupCoordinates(Loc.GetString("lavaland-node-funny-crash"), Transform(node).Coordinates, PopupType.Medium);
                }

                QueueDel(node);
            });
        });
    }

    /// <summary>node_drone/death: небольшой взрыв.</summary>
    private void OnDroneStateChanged(Entity<LavalandNodeDroneComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "Default", 10f, 5f, 4f, ent, canCreateVacuum: false);
        QueueDel(ent);
    }

    #endregion

    #region Boulders

    private int CountBoulders(EntityUid vent)
    {
        return _lookup.GetEntitiesInRange<LavalandBoulderComponent>(Transform(vent).Coordinates, 0.5f).Count;
    }

    /// <summary>produce_boulder: 3 броска по минералам, количество убывает логарифмически.</summary>
    private void ProduceBoulder(Entity<LavalandOreVentComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Minerals.Count == 0)
            return;

        var artifact = _random.Prob(comp.ArtifactChance);
        var rock = Spawn(artifact ? comp.ArtifactBoulder : comp.Boulder, Transform(ent).Coordinates);
        var boulder = EnsureComp<LavalandBoulderComponent>(rock);
        var size = (int) comp.Size;
        for (var i = 1; i <= MineralsPerBoulder; i++)
        {
            var mineral = PickWeighted(comp.Minerals);
            // ore_quantity_function: HALF_SHEET * max(round(size / ln(rand(1+i, 4+i))), 1)
            var units = 50 * Math.Max((int) MathF.Round(size / MathF.Log(_random.Next(1 + i, 5 + i))), 1);
            boulder.Materials[mineral] = boulder.Materials.GetValueOrDefault(mineral) + units;
        }

        boulder.Size = comp.Size;
        boulder.Durability = _random.Next(2, size + 1);
        boulder.Style = comp.BoulderStyle;
        boulder.PlatformLifespan = PlatformLife(comp.Size);
        if (!artifact)
            _appearance.SetData(rock, MegafaunaVisuals.State, $"{boulder.Style}_{SizeName(comp.Size)}");
    }

    private void OnBoulderExamined(Entity<LavalandBoulderComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("lavaland-boulder-durability", ("steps", ent.Comp.Durability)));
    }

    private void OnBoulderInteractUsing(Entity<LavalandBoulderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, PickaxeTag))
            return;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("lavaland-boulder-swing", ("boulder", ent.Owner)), ent, args.User);
        StartBreak(ent, args.User, args.Used);
    }

    private void StartBreak(Entity<LavalandBoulderComponent> ent, EntityUid user, EntityUid tool)
    {
        _ai.PlaySound(ent.Comp.HitSound, Transform(ent).Coordinates, -5f);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, 2f, new BoulderBreakDoAfterEvent(), ent, ent, tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    /// <summary>manual_process: шаг разбивки; на нуле — руда.</summary>
    private void OnBoulderBreak(Entity<LavalandBoulderComponent> ent, ref BoulderBreakDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } tool)
            return;
        args.Handled = true;

        ent.Comp.Durability--;
        if (ent.Comp.Durability > 0)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.Durability == 1 ? "lavaland-boulder-crumbling" : "lavaland-boulder-weaker", ("boulder", ent.Owner)), ent, args.User);
            StartBreak(ent, args.User, tool);
            return;
        }

        ConvertToOre(ent);
        _popup.PopupEntity(Loc.GetString("lavaland-boulder-broken", ("boulder", ent.Owner)), ent, args.User);
        _ai.PlaySound(ent.Comp.BreakSound, Transform(ent).Coordinates);
        QueueDel(ent);
    }

    /// <summary>convert_to_ore: (материал - лист) / лист руды, от 1 до 10.</summary>
    private void ConvertToOre(Entity<LavalandBoulderComponent> ent)
    {
        var coords = Transform(ent).Coordinates;
        foreach (var (ore, units) in ent.Comp.Materials)
        {
            var quantity = Math.Clamp((int) MathF.Round((units - 100) / 100f), 1, 10);
            var stack = Spawn(ore, coords);
            if (TryComp<StackComponent>(stack, out var stackComp))
                _stack.SetCount((stack, stackComp), quantity);
        }

        if (ent.Comp.Artifact is { } artifact)
            Spawn(artifact, coords);
    }

    /// <summary>interact_with_atom(lava): валун становится временным плотом.</summary>
    private void OnBoulderAfterInteract(Entity<LavalandBoulderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_ai.TryGetTile(args.ClickLocation, out var grid, out var tile) || !HasLava(grid, tile))
            return;
        args.Handled = TryCreatePlatform(ent, grid, tile, args.User);
    }

    private void OnBoulderLand(Entity<LavalandBoulderComponent> ent, ref LandEvent args)
    {
        if (_ai.TryGetTile(ent, out var grid, out var tile) && HasLava(grid, tile))
            TryCreatePlatform(ent, grid, tile, args.User);
    }

    private bool TryCreatePlatform(Entity<LavalandBoulderComponent> ent, Entity<MapGridComponent> grid, Vector2i tile, EntityUid? user)
    {
        if (HasPlatform(grid, tile))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("lavaland-boulder-platform-exists"), ent, user.Value);
            return false;
        }

        SpawnPlatform(ent.Comp.Platform, grid, tile, ent.Comp.PlatformLifespan);
        _popup.PopupCoordinates(Loc.GetString("lavaland-boulder-platform", ("boulder", ent.Owner)), _ai.TileCenter(grid, tile));
        QueueDel(ent);
        return true;
    }

    private void SpawnPlatform(EntProtoId proto, Entity<MapGridComponent> grid, Vector2i tile, float lifespan)
    {
        var platform = _ai.SpawnAt(proto, grid, tile);
        RemComp<TimedDespawnComponent>(platform);
        // catwalk/boulder/self_destruct: тонет с шипением.
        _ai.Schedule(null, lifespan, () =>
        {
            if (TerminatingOrDeleted(platform))
                return;
            _popup.PopupEntity(Loc.GetString("lavaland-boulder-platform-sinks", ("platform", platform)), platform, PopupType.Small);
            _ai.PlaySound(_platformSinkSound, Transform(platform).Coordinates, -8f);
            QueueDel(platform);
        });
    }

    private bool HasLava(Entity<MapGridComponent> grid, Vector2i tile)
    {
        return _map.GetAnchoredEntities(grid, grid.Comp, tile).Any(HasComp<TileEntityEffectComponent>);
    }

    private bool HasPlatform(Entity<MapGridComponent> grid, Vector2i tile)
    {
        return _map.GetAnchoredEntities(grid, grid.Comp, tile).Any(e => MetaData(e).EntityPrototype?.ID == "ImperialBoulderPlatform");
    }

    #endregion
}
