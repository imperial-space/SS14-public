using System.Linq;
using Content.Server.Destructible;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Doors;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Постройки культа (cult_structures*.dm), рунный металл (runed_metal.dm), рунные двери/балки, пол культа, сокрытие.
/// </summary>
public sealed partial class CultStructureSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly Content.Shared.Damage.Systems.DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly CultRuneSystem _runes = default!;
    [Dependency] private readonly CultBloodMagicSystem _magic = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBloodstreamSystem _blood = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public const string CultFloor = "FloorCult";
    private static readonly EntProtoId PylonHealEffect = "CultPylonHealEffect";
    private static readonly string[] PylonBlacklistTiles = { "FloorAsteroidSand", "FloorAsteroidTile", "FloorAsteroidIronsand", "FloorCult" };

    /// <summary>Рецепты рунного металла (runed_metal_recipes).</summary>
    private static readonly (string Id, EntProtoId Result, int Cost, float Time, string Icon)[] Recipes =
    {
        ("pylon", "CultPylon", 4, 4f, "pylon"),
        ("altar", "CultAltar", 3, 4f, "talismanaltar"),
        ("archives", "CultArchives", 3, 4f, "tomealtar"),
        ("forge", "CultForge", 3, 4f, "forge"),
        ("door", "CultRunedAirlock", 1, 5f, "door"),
        ("girder", "CultRunedGirder", 1, 5f, "cultgirder"),
    };

    private readonly Dictionary<EntityUid, TimeSpan> _doorCooldown = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultStructureComponent, MapInitEvent>(OnStructureInit);
        SubscribeLocalEvent<CultStructureComponent, ExaminedEvent>(OnStructureExamined);
        SubscribeLocalEvent<CultStructureComponent, ActivateInWorldEvent>(OnStructureActivate);
        SubscribeLocalEvent<CultStructureComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CultStructureComponent, DestructionEventArgs>(OnStructureDestroyed);

        SubscribeLocalEvent<CultRunedMetalComponent, UseInHandEvent>(OnRunedMetalUse);
        SubscribeLocalEvent<CultRunedMetalComponent, AfterInteractEvent>(OnRunedMetalInteract);
        SubscribeLocalEvent<CultistComponent, CultRunedMetalDoAfterEvent>(OnRunedMetalDoAfter);

        SubscribeLocalEvent<CultRunedDoorComponent, MapInitEvent>(OnDoorInit);
        SubscribeLocalEvent<CultRunedDoorComponent, BeforeDoorOpenedEvent>(OnDoorOpening);
    }

    #region Постройки

    private void OnStructureInit(Entity<CultStructureComponent> ent, ref MapInitEvent args)
    {
        UpdateAnchoredVisuals(ent);
    }

    private void OnAnchorChanged(Entity<CultStructureComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateAnchoredVisuals(ent);
    }

    private void UpdateAnchoredVisuals(Entity<CultStructureComponent> ent)
    {
        var anchored = Transform(ent).Anchored;
        _appearance.SetData(ent, CultVisuals.Anchored, anchored);
        if (!ent.Comp.Concealed && TryComp<PointLightComponent>(ent, out var light))
            _light.SetEnabled(ent, anchored && ent.Comp.StructureType != CultStructureType.Altar, light);
    }

    private void OnStructureDestroyed(Entity<CultStructureComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.BreakMessage is { } message)
            _popup.PopupCoordinates(Loc.GetString(message), Transform(ent).Coordinates, PopupType.MediumCaution);
    }

    private void OnStructureExamined(Entity<CultStructureComponent> ent, ref ExaminedEvent args)
    {
        var anchored = Transform(ent).Anchored;
        args.PushMarkup(Loc.GetString(anchored ? "cult-structure-secured" : "cult-structure-unsecured", ("structure", ent)));

        if (!_cult.IsCultAligned(args.Examiner) && !HasComp<Content.Shared.Ghost.GhostComponent>(args.Examiner))
            return;

        if (TryComp<DamageableComponent>(ent, out var damageable))
        {
            var max = _destructible.DestroyedAt(ent).Float();
            if (max > 0)
            {
                var percent = (int) MathF.Round((max - _cult.TotalDamage(ent)) * 100f / max);
                args.PushMarkup(Loc.GetString("cult-structure-stability", ("percent", percent)));
            }
        }
        if (ent.Comp.ExamineTip is { } tip)
            args.PushMarkup(Loc.GetString(tip));
        var left = ent.Comp.NextUse - _timing.CurTime;
        if (left > TimeSpan.Zero)
            args.PushMarkup(Loc.GetString("cult-structure-cooldown", ("structure", ent), ("time", FormatTime(left))));
    }

    public static string FormatTime(TimeSpan time)
    {
        return time.TotalMinutes >= 1
            ? $"{(int) time.TotalMinutes}:{time.Seconds:00}"
            : $"{(int) Math.Ceiling(time.TotalSeconds)} с";
    }

    private void OnStructureActivate(Entity<CultStructureComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || ent.Comp.Options.Count == 0)
            return;
        args.Handled = true;
        var user = args.User;

        if (!_cult.IsCultist(user))
        {
            _popup.PopupEntity(Loc.GetString("cult-structure-cant-touch"), user, user);
            return;
        }
        if (!Transform(ent).Anchored)
        {
            _cult.Message(user, Loc.GetString("cult-structure-anchor-first", ("structure", ent)));
            return;
        }
        var left = ent.Comp.NextUse - _timing.CurTime;
        if (left > TimeSpan.Zero)
        {
            _cult.Message(user, Loc.GetString("cult-structure-cooldown", ("structure", ent), ("time", FormatTime(left))));
            return;
        }

        var options = ent.Comp.Options.Select((o, i) =>
            new CultMenuOption(i.ToString(), Loc.GetString(o.Name), o.Icon, Loc.GetString(o.Description))).ToList();

        _cult.OpenChoice(user, Name(ent), options, id =>
        {
            if (!int.TryParse(id, out var index) || index < 0 || index >= ent.Comp.Options.Count)
                return;
            if (TerminatingOrDeleted(ent) || !Transform(ent).Anchored || !InRange(user, ent) || !_cult.IsCultist(user)
                || _mobState.IsIncapacitated(user) || ent.Comp.NextUse > _timing.CurTime)
                return;

            ent.Comp.NextUse = _timing.CurTime + ent.Comp.UseCooldown;
            foreach (var proto in ent.Comp.Options[index].Items)
            {
                var item = Spawn(proto, Transform(ent).Coordinates);
                var message = ent.Comp.SuccessMessage ?? "cult-structure-produces";
                _cult.Message(user, Loc.GetString(message, ("structure", ent), ("item", item)));
            }
        });
    }

    private bool InRange(EntityUid user, EntityUid target)
    {
        var a = _transform.GetMapCoordinates(user);
        var b = _transform.GetMapCoordinates(target);
        return a.MapId == b.MapId && (a.Position - b.Position).Length() <= 1.6f;
    }

    /// <summary>Удар ритуальным кинжалом: балка рушится, постройка (от)крепляется.</summary>
    public bool TryRitualHit(EntityUid target, EntityUid user, EntityUid item)
    {
        if (HasComp<CultGirderComponent>(target))
        {
            _audio.PlayPvs(CultSounds.ResonatorBlast, target, AudioParams.Default.WithVolume(CultSystem.Db(40)).WithVariation(0.05f));
            _popup.PopupEntity(Loc.GetString("cult-girder-strike-others", ("user", user), ("target", target), ("item", item)), user, Filter.PvsExcept(user), true);
            _popup.PopupEntity(Loc.GetString("cult-girder-strike-self", ("target", target)), user, user);
            Spawn("CultRunedMetal", Transform(target).Coordinates);
            QueueDel(target);
            return true;
        }

        if (HasComp<CultStructureComponent>(target))
        {
            _audio.PlayPvs(CultSounds.Deconstruct, target, AudioParams.Default.WithVolume(CultSystem.Db(30)).WithVariation(0.05f));
            var xform = Transform(target);
            if (xform.Anchored)
                _transform.Unanchor(target, xform);
            else
                _transform.AnchorEntity((target, xform));
            _cult.Message(user, Loc.GetString(Transform(target).Anchored ? "cult-structure-secure" : "cult-structure-unsecure", ("structure", target)));
            return true;
        }

        return false;
    }

    #endregion

    #region Пилон

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        UpdateHolyWater(now);

        var query = EntityQueryEnumerator<CultPylonComponent, CultStructureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pylon, out var structure, out var xform))
        {
            if (structure.Concealed)
                continue;

            if (now >= pylon.NextHeal)
            {
                pylon.NextHeal = now + TimeSpan.FromSeconds(1);
                PylonHeal(uid, pylon);
            }

            if (!xform.Anchored || now < pylon.NextCorruption)
                continue;
            var found = PylonCorrupt(uid, pylon, xform);
            pylon.NextCorruption = now + (found ? pylon.CorruptionCooldown : pylon.CorruptionCooldown * 2);
        }
    }

    /// <summary>datum/component/aura_healing для культистов и конструктов.</summary>
    private void PylonHeal(EntityUid pylon, CultPylonComponent comp)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(pylon).Coordinates, comp.Range))
        {
            if (!_cult.IsCultAligned(mob) || _mobState.IsDead(mob))
                continue;

            // temp_visual/heal цвета культа раз в секунду, если есть что лечить.
            if (TryComp<DamageableComponent>(mob, out var damageable) && _damageable.GetTotalDamage((mob, damageable)) > 0)
                Spawn(PylonHealEffect, Transform(mob).Coordinates.Offset(new System.Numerics.Vector2(_random.NextFloat(-0.375f, 0.375f), _random.NextFloat(-0.28f, 0f))));

            if (HasComp<CultConstructComponent>(mob) || HasComp<CultShadeComponent>(mob))
            {
                _cult.HealAll(mob, comp.SimpleHeal);
                continue;
            }

            _cult.HealGroup(mob, "Brute", comp.BruteHeal);
            _cult.HealGroup(mob, "Burn", comp.BurnHeal);
            if (HasComp<BloodstreamComponent>(mob))
            {
                if (_blood.GetBloodLevel(mob.Owner) < 1f)
                    _magic.ModifyBloodSs13(mob, comp.BloodHeal);
                // wound_clotting: раны перестают кровоточить.
                _blood.TryModifyBleedAmount(mob.Owner, -comp.WoundClotting);
            }
        }
    }

    /// <summary>pylon/process(): каждые 5 с один тайл в радиусе 5 становится полом культа.</summary>
    private bool PylonCorrupt(EntityUid pylon, CultPylonComponent comp, TransformComponent xform)
    {
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var center = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var valid = new List<Vector2i>();
        var cult = new List<Vector2i>();
        var range = (int) comp.Range;
        var origin = _transform.GetMapCoordinates(pylon);

        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                if (x * x + y * y > range * range)
                    continue;
                var idx = center + new Vector2i(x, y);
                if (!_map.TryGetTileRef(grid, gridComp, idx, out var tile) || tile.Tile.IsEmpty || _turf.IsSpace(tile))
                    continue;

                var def = (ContentTileDefinition) _tiles[tile.Tile.TypeId];
                var coords = _map.GridTileToLocal(grid, gridComp, idx);
                if (def.ID == CultFloor)
                {
                    cult.Add(idx);
                    continue;
                }
                if (PylonBlacklistTiles.Contains(def.ID) || def.ID.StartsWith("FloorAsteroid") || def.ID.StartsWith("Lattice"))
                    continue;
                if (IsWall(coords))
                    continue;
                if (!_examine.InRangeUnOccluded(origin, _transform.ToMapCoordinates(coords), comp.Range + 1, null))
                    continue;
                valid.Add(idx);
            }
        }

        if (valid.Count > 0)
        {
            var idx = _random.Pick(valid);
            _map.SetTile(grid, gridComp, idx, new Tile(_tiles[CultFloor].TileId));
            return true;
        }

        if (cult.Count > 0)
        {
            var idx = _random.Pick(cult);
            _cult.Effect("CultEffectFloorGlow", _map.GridTileToLocal(grid, gridComp, idx));
            return true;
        }

        return false;
    }

    private bool IsWall(EntityCoordinates coords)
    {
        var tile = _turf.GetTileRef(coords);
        return tile != null && _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable | CollisionGroup.Opaque);
    }

    public bool IsBlocked(EntityCoordinates coords)
    {
        var tile = _turf.GetTileRef(coords);
        return tile == null || _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }

    /// <summary>Есть ли на тайле плотная преграда (космос не преграда).</summary>
    public bool IsDense(EntityCoordinates coords)
    {
        var tile = _turf.GetTileRef(coords);
        return tile != null && _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }

    public bool IsCultFloor(EntityCoordinates coords)
    {
        var tile = _turf.GetTileRef(coords);
        return tile != null && !tile.Value.Tile.IsEmpty && _tiles[tile.Value.Tile.TypeId].ID == CultFloor;
    }

    /// <summary>turf/narsie_act(): пол становится полом культа.</summary>
    public void NarsieActTurf(EntityCoordinates coords) => MakeCultFloor(coords);

    /// <summary>Тайл пола культа для замены или null, если тайл уже культа, космос или пустой.</summary>
    public Tile? CultFloorTileFor(TileRef tile)
    {
        if (tile.Tile.IsEmpty || _turf.IsSpace(tile) || _tiles[tile.Tile.TypeId].ID == CultFloor)
            return null;
        return new Tile(_tiles[CultFloor].TileId);
    }

    /// <summary>Превратить тайл в пол культа (narsie_act пола).</summary>
    public void MakeCultFloor(EntityCoordinates coords)
    {
        var grid = _transform.GetGrid(coords);
        if (grid == null || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;
        var idx = _map.TileIndicesFor(grid.Value, gridComp, coords);
        if (!_map.TryGetTileRef(grid.Value, gridComp, idx, out var tile) || tile.Tile.IsEmpty || _turf.IsSpace(tile))
            return;
        if (_tiles[tile.Tile.TypeId].ID == CultFloor)
            return;
        _map.SetTile(grid.Value, gridComp, idx, new Tile(_tiles[CultFloor].TileId));
    }

    #endregion

    #region Рунный металл

    private void OnRunedMetalUse(Entity<CultRunedMetalComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.User;

        if (!_cult.IsCultist(user))
        {
            _cult.Message(user, Loc.GetString("cult-runed-metal-forbidden"));
            return;
        }
        if (!_runes.VeilWeak(user))
        {
            _cult.Message(user, Loc.GetString("cult-scribe-veil"));
            return;
        }

        var structures = new ResPathSpecifier("/Textures/Imperial/BloodCult/structures.rsi");
        var options = Recipes.Select(r =>
        {
            Robust.Shared.Utility.SpriteSpecifier icon = r.Id == "door"
                ? new Robust.Shared.Utility.SpriteSpecifier.Rsi(new Robust.Shared.Utility.ResPath("/Textures/Imperial/cult/air.rsi"), "closed")
                : new Robust.Shared.Utility.SpriteSpecifier.Rsi(structures.Path, r.Icon);
            return new CultMenuOption(r.Id, Loc.GetString($"cult-recipe-{r.Id}", ("cost", r.Cost)), icon, Loc.GetString($"cult-recipe-{r.Id}-desc"));
        }).ToList();

        _cult.OpenChoice(user, Loc.GetString("cult-runed-metal-title"), options, id =>
        {
            var recipe = Recipes.FirstOrDefault(r => r.Id == id);
            if (recipe.Id == null || TerminatingOrDeleted(ent) || !_hands.IsHolding(user, ent) || !_cult.IsCultist(user))
                return;
            if (!TryComp<StackComponent>(ent, out var stack) || stack.Count < recipe.Cost)
            {
                _cult.Message(user, Loc.GetString("cult-runed-metal-not-enough", ("cost", recipe.Cost)));
                return;
            }
            if (!CanBuildHere(user))
                return;

            var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(recipe.Time),
                new CultRunedMetalDoAfterEvent { Result = recipe.Result, Cost = recipe.Cost }, user, used: ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            };
            _doAfter.TryStartDoAfter(doAfter);
        });
    }

    /// <summary>CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND.</summary>
    private bool CanBuildHere(EntityUid user)
    {
        var coords = Transform(user).Coordinates;
        var tile = _turf.GetTileRef(coords);
        if (tile == null || _turf.IsSpace(tile.Value))
        {
            _cult.Message(user, Loc.GetString("cult-runed-metal-solid-ground"));
            return false;
        }
        foreach (var ent in _lookup.GetEntitiesInRange(coords, 0.4f, LookupFlags.Static))
        {
            if (TryComp<PhysicsComponent>(ent, out var physics) && physics.Hard && physics.CanCollide && Transform(ent).Anchored)
            {
                _cult.Message(user, Loc.GetString("cult-runed-metal-occupied"));
                return false;
            }
        }
        return true;
    }

    private void OnRunedMetalDoAfter(Entity<CultistComponent> ent, ref CultRunedMetalDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used || !TryComp<StackComponent>(used, out var stack))
            return;
        args.Handled = true;

        if (args.Result == "WallCult")
        {
            if (args.Target is not { } girder || TerminatingOrDeleted(girder) || stack.Count < 1)
                return;
            _stack.ReduceCount((used, stack), 1);
            _popup.PopupEntity(Loc.GetString("cult-girder-plated-others", ("user", ent), ("target", girder)), ent, Filter.PvsExcept(ent), true);
            _popup.PopupEntity(Loc.GetString("cult-girder-plated-self"), ent, ent);
            var coords = Transform(girder).Coordinates;
            QueueDel(girder);
            Spawn("WallCult", coords);
            return;
        }

        if (stack.Count < args.Cost || !CanBuildHere(ent))
            return;
        _stack.ReduceCount((used, stack), args.Cost);

        var grid = _transform.GetGrid(Transform(ent).Coordinates);
        var spawnAt = Transform(ent).Coordinates;
        if (grid != null && TryComp<MapGridComponent>(grid, out var gridComp))
            spawnAt = _map.GridTileToLocal(grid.Value, gridComp, _map.TileIndicesFor(grid.Value, gridComp, spawnAt));
        Spawn(args.Result, spawnAt);
    }

    /// <summary>Рунный металл на рунную балку — рунная стена (girder/cult/attackby).</summary>
    private void OnRunedMetalInteract(Entity<CultRunedMetalComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<CultGirderComponent>(target))
            return;
        args.Handled = true;
        if (!TryComp<StackComponent>(ent, out var stack) || stack.Count < 1)
        {
            _cult.Message(args.User, Loc.GetString("cult-girder-need-sheet"));
            return;
        }

        _popup.PopupEntity(Loc.GetString("cult-girder-plating-others", ("user", args.User), ("target", target)), args.User, Filter.PvsExcept(args.User), true);
        _popup.PopupEntity(Loc.GetString("cult-girder-plating-self"), args.User, args.User);
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5),
            new CultRunedMetalDoAfterEvent { Result = "WallCult", Cost = 1 }, args.User, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    #endregion

    #region Рунная дверь

    private void OnDoorInit(Entity<CultRunedDoorComponent> ent, ref MapInitEvent args)
    {
        _cult.Effect(ent.Comp.Runed ? "CultEffectDoorGlow" : "CultEffectDoorGlowUnruned", Transform(ent).Coordinates);
    }

    /// <summary>airlock/cult/allowed(): не-культиста отбрасывает и оглушает.</summary>
    private void OnDoorOpening(Entity<CultRunedDoorComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (args.User is not { } user)
            return;

        if (ent.Comp.Friendly || _cult.IsCultAligned(user))
        {
            if (!ent.Comp.Concealed)
                _cult.Effect(ent.Comp.Runed ? "CultEffectDoorGlow" : "CultEffectDoorGlowUnruned", Transform(ent).Coordinates);
            return;
        }

        args.Cancel();
        if (ent.Comp.Concealed)
            return;

        var now = _timing.CurTime;
        if (_doorCooldown.TryGetValue(user, out var next) && now < next)
            return;
        _doorCooldown[user] = now + TimeSpan.FromSeconds(1);

        _cult.Effect("CultEffectSacrifice", Transform(ent).Coordinates);
        _cult.SoundTo(user, CultSounds.TurnAround, CultSystem.Db(50));
        _cult.Paralyze(user, 4);

        var doorPos = _transform.GetWorldPosition(ent);
        var userPos = _transform.GetWorldPosition(user);
        var dir = userPos - doorPos;
        if (dir.LengthSquared() < 0.01f)
            dir = new System.Numerics.Vector2(0, -1);
        dir = System.Numerics.Vector2.Normalize(dir);
        _throwing.TryThrow(user, Transform(user).Coordinates.Offset(dir * 5f), 10f, ent);
    }

    /// <summary>airlock/narsie_act(): шлюз становится хрупким рунным.</summary>
    public void NarsieActAirlock(EntityUid airlock)
    {
        var xform = Transform(airlock);
        var name = Name(airlock);
        var coords = xform.Coordinates;
        var rot = xform.LocalRotation;
        QueueDel(airlock);
        var door = Spawn("CultRunedAirlockBrittle", coords);
        _transform.SetLocalRotation(door, rot);
        _cult.SetName(door, name);
    }

    #endregion

    #region Сокрытие

    /// <summary>veiling: скрыть руны, постройки и двери в радиусе.</summary>
    public int ConcealAround(MapCoordinates center, float range)
    {
        var count = 0;
        foreach (var rune in _lookup.GetEntitiesInRange<CultRuneComponent>(center, range))
        {
            if (rune.Comp.Concealed || rune.Comp.RuneType == CultRuneType.NarSie)
                continue;
            SetRuneConcealed(rune, true);
            count++;
        }
        foreach (var structure in _lookup.GetEntitiesInRange<CultStructureComponent>(center, range))
        {
            if (structure.Comp.Concealed)
                continue;
            SetStructureConcealed(structure, true);
            count++;
        }
        foreach (var door in _lookup.GetEntitiesInRange<CultRunedDoorComponent>(center, range))
        {
            door.Comp.Concealed = true;
            _appearance.SetData(door, CultVisuals.Concealed, true);
            count++;
        }
        return count;
    }

    /// <summary>reveal: руны (range), постройки и двери (structureRange).</summary>
    public int RevealAround(MapCoordinates center, float range, float? structureRange = null)
    {
        var sRange = structureRange ?? range;
        var count = 0;
        foreach (var rune in _lookup.GetEntitiesInRange<CultRuneComponent>(center, range))
        {
            if (!rune.Comp.Concealed)
                continue;
            SetRuneConcealed(rune, false);
            count++;
        }
        foreach (var structure in _lookup.GetEntitiesInRange<CultStructureComponent>(center, sRange))
        {
            if (!structure.Comp.Concealed)
                continue;
            SetStructureConcealed(structure, false);
            count++;
        }
        foreach (var door in _lookup.GetEntitiesInRange<CultRunedDoorComponent>(center, sRange))
        {
            if (!door.Comp.Concealed)
                continue;
            door.Comp.Concealed = false;
            _appearance.SetData(door, CultVisuals.Concealed, false);
            count++;
        }
        return count;
    }


    private void SetRuneConcealed(Entity<CultRuneComponent> rune, bool concealed)
    {
        rune.Comp.Concealed = concealed;
        _popup.PopupEntity(Loc.GetString(concealed ? "cult-conceal-fades" : "cult-conceal-appears", ("target", rune)), rune, PopupType.SmallCaution);
        var vis = EnsureComp<VisibilityComponent>(rune);
        _visibility.SetLayer((rune, vis), (ushort) (concealed ? VisibilityFlags.Ghost : VisibilityFlags.Normal));
        _appearance.SetData(rune, CultVisuals.Concealed, concealed);
    }

    private void SetStructureConcealed(Entity<CultStructureComponent> structure, bool concealed)
    {
        structure.Comp.Concealed = concealed;
        _popup.PopupEntity(Loc.GetString(concealed ? "cult-conceal-fades" : "cult-conceal-appears", ("target", structure)), structure, PopupType.SmallCaution);
        if (TryComp<PhysicsComponent>(structure, out var physics))
            _physics.SetCanCollide(structure, !concealed, body: physics);
        var vis = EnsureComp<VisibilityComponent>(structure);
        _visibility.SetLayer((structure, vis), (ushort) (concealed ? VisibilityFlags.Ghost : VisibilityFlags.Normal));
        _appearance.SetData(structure, CultVisuals.Concealed, concealed);
        if (TryComp<PointLightComponent>(structure, out var light))
            _light.SetEnabled(structure, !concealed && Transform(structure).Anchored, light);
    }

    #endregion

    /// <summary>dust(): тело рассыпается пеплом, вещи падают.</summary>
    public void DustBody(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;
        if (_inventory.TryGetContainerSlotEnumerator(uid, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } item)
                {
                    if (HasComp<CultGhostItemComponent>(item))
                        QueueDel(item);
                    else
                        _transform.DropNextTo(item, uid);
                }
            }
        }
        foreach (var held in _hands.EnumerateHeld(uid).ToList())
        {
            if (HasComp<CultGhostItemComponent>(held))
                QueueDel(held);
            else
                _hands.TryDrop(uid, held, checkActionBlocker: false);
        }
        Spawn("Ash", coords);
        QueueDel(uid);
    }
}

/// <summary>Путь к RSI построек (для иконок меню).</summary>
internal readonly record struct ResPathSpecifier(string Value)
{
    public Robust.Shared.Utility.ResPath Path => new(Value);
}
