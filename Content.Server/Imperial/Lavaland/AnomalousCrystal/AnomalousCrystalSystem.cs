using System.Linq;
using Content.Server.Clothing.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Shared.Actions;
using Content.Shared.Administration.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Imperial.Lavaland.AnomalousCrystal;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Content.Shared.Weapons.Melee;
using Content.Server.Gatherable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.AnomalousCrystal;

/// <summary>Перенос machinery/anomalous_crystal и его подтипов из SS13.</summary>
public sealed class AnomalousCrystalSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly OutfitSystem _outfit = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const string PossessorContainer = "anomalous_possessor";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    /// <summary>dimension_theme: пол и стены (null — не менять).</summary>
    private static readonly (string Name, string Floor, string? Wall)[] Themes =
    {
        ("Gold", "FloorGold", "WallGold"),
        ("Plasma", "FloorDark", "WallPlasma"),
        ("Clown", "FloorClown", "WallClown"),
        ("Radioactive", "FloorDark", "WallUranium"),
        ("Meat", "FloorFlesh", "WallMeat"),
        ("Natural", "FloorGrass", "WallWood"),
        ("Winter", "FloorSnow", "WallIce"),
        ("Lavaland", "FloorBasalt", "WallCult"),
        ("Glass", "FloorGlass", null),
        ("Fancy", "FloorWood", "WallWood"),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnomalousCrystalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnomalousCrystalComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<AnomalousCrystalComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AnomalousPossessedComponent, ExitPossessionActionEvent>(OnExitPossession);
        SubscribeLocalEvent<AnomalousPossessedComponent, MobStateChangedEvent>(OnPossessedStateChanged);
        SubscribeLocalEvent<AnomalousPossessorBodyComponent, BeforeDamageChangedEvent>(OnBodyDamage);
    }

    private void OnMapInit(Entity<AnomalousCrystalComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Kind == AnomalousCrystalKind.ThemeWarp)
            ent.Comp.ThemeIndex = _random.Next(Themes.Length);
    }

    private void OnExamined(Entity<AnomalousCrystalComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<GhostComponent>(args.Examiner))
            return;

        args.PushMarkup(ent.Comp.Kind == AnomalousCrystalKind.ThemeWarp && ent.Comp.ThemeIndex >= 0
            ? Loc.GetString("anomalous-crystal-observer-theme", ("theme", Themes[ent.Comp.ThemeIndex].Name))
            : Loc.GetString(ent.Comp.ObserverDesc));
    }

    /// <summary>attack_hand -> ActivationReaction(ACTIVATE_TOUCH).</summary>
    private void OnInteractHand(Entity<AnomalousCrystalComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (_timing.CurTime < ent.Comp.NextUse || ent.Comp.Active)
            return;

        var user = args.User;
        if (ent.Comp.UseTime > 0)
        {
            // charge_animation
            ent.Comp.Active = true;
            _transform.AnchorEntity(ent, Transform(ent));
            _appearance.SetData(ent, MegafaunaVisuals.State, "anomaly_crystal_charging");
            _popup.PopupEntity(Loc.GetString("anomalous-crystal-charging"), ent, PopupType.Medium);
            _ai.PlaySound(ent.Comp.ChargeSound, Transform(ent).Coordinates);
            _ai.Schedule(null, ent.Comp.UseTime, () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;
                _appearance.SetData(ent, MegafaunaVisuals.State, string.Empty);
                ent.Comp.Active = false;
                _transform.Unanchor(ent, Transform(ent));
                Activate(ent, user);
            });
            return;
        }

        Activate(ent, user);
    }

    private void Activate(Entity<AnomalousCrystalComponent> ent, EntityUid user)
    {
        ent.Comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Cooldown);
        _ai.PlaySound(ent.Comp.ActivationSound, Transform(user).Coordinates, 5f);

        switch (ent.Comp.Kind)
        {
            case AnomalousCrystalKind.Honk:
                Honk(ent);
                break;
            case AnomalousCrystalKind.ThemeWarp:
                ThemeWarp(ent);
                break;
            case AnomalousCrystalKind.Emitter:
                var angle = 180f - (float) Transform(ent).LocalRotation.Degrees;
                _ai.ShootProjectile(ent, ent.Comp.Projectile, angle, 10f);
                break;
            case AnomalousCrystalKind.DarkReprise:
                DarkReprise(ent);
                break;
            case AnomalousCrystalKind.Helpers:
                Helpers(ent);
                break;
            case AnomalousCrystalKind.Possessor:
                Possess(ent, user);
                break;
        }
    }

    #region Revives

    private IEnumerable<EntityUid> DeadHumansAround(EntityUid crystal)
    {
        if (!_ai.TryGetTile(crystal, out var grid, out var center))
            yield break;

        foreach (var tile in MegafaunaAiSystem.RangeTiles(center, 1))
        {
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                if (HasComp<HumanoidProfileComponent>(mob) && _mobState.IsDead(mob))
                    yield return mob;
            }
        }
    }

    private void SpawnAround(EntityUid crystal, string proto)
    {
        if (!_ai.TryGetTile(crystal, out var grid, out var center))
            return;
        foreach (var tile in MegafaunaAiSystem.RangeTiles(center, 1))
            _ai.SpawnAt(proto, grid, tile);
    }

    /// <summary>revive(ADMIN_HEAL_ALL, force_grab_ghost = TRUE).</summary>
    private void Revive(EntityUid body)
    {
        _rejuvenate.PerformRejuvenate(body);
        if (_mind.TryGetMind(body, out var mindId, out var mind) && mind.VisitingEntity != null)
            _mind.UnVisit(mindId, mind);
    }

    /// <summary>honk: оживляет мёртвых рядом и переодевает в клоуна (один раз на человека).</summary>
    private void Honk(Entity<AnomalousCrystalComponent> ent)
    {
        var dead = DeadHumansAround(ent).ToList();
        SpawnAround(ent, ent.Comp.Confetti);
        foreach (var body in dead)
        {
            Revive(body);
            if (!ent.Comp.Clowned.Add(body))
                continue;

            if (_inventory.TryGetSlots(body, out var slots))
            {
                foreach (var slot in slots)
                    _inventory.TryUnequip(body, slot.Name, true, true);
            }

            _outfit.SetOutfit(body, ent.Comp.ClownGear);
        }
    }

    /// <summary>dark_reprise: оживляет мёртвых рядом, но дефибриллятор их больше не поднимет.</summary>
    private void DarkReprise(Entity<AnomalousCrystalComponent> ent)
    {
        var dead = DeadHumansAround(ent).ToList();
        SpawnAround(ent, ent.Comp.Sparks);
        foreach (var body in dead)
        {
            Revive(body);
            EnsureComp<UnrevivableComponent>(body);
            _popup.PopupEntity(Loc.GetString("anomalous-crystal-dark-reprise"), body, body, PopupType.LargeCaution);
        }
    }

    #endregion

    #region Theme warp

    /// <summary>theme_warp: пол и стены вокруг кристалла превращаются в тему (один раз на место).</summary>
    private void ThemeWarp(Entity<AnomalousCrystalComponent> ent)
    {
        if (ent.Comp.ThemeIndex < 0 || !_ai.TryGetTile(ent, out var grid, out var center))
            return;

        foreach (var (g, t) in ent.Comp.ConvertedCenters)
        {
            if (g == grid.Owner && MegafaunaAiSystem.Chebyshev(t, center) <= ent.Comp.ThemeRadius)
                return;
        }

        ent.Comp.ConvertedCenters.Add((grid.Owner, center));
        var theme = Themes[ent.Comp.ThemeIndex];
        var floor = _tileDefs[theme.Floor];

        foreach (var tile in MegafaunaAiSystem.RangeTiles(center, ent.Comp.ThemeRadius))
        {
            if (!_map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var wall = _map.GetAnchoredEntities(grid, grid.Comp, tile)
                .FirstOrDefault(e => _tag.HasTag(e, WallTag) && !HasComp<GatherableComponent>(e));
            if (wall.Valid)
            {
                if (theme.Wall is { } wallProto && MetaData(wall).EntityPrototype?.ID != wallProto)
                {
                    QueueDel(wall);
                    _ai.SpawnAt(wallProto, grid, tile);
                }

                continue;
            }

            if (_ai.IsMineral(grid, tile))
                continue;

            _map.SetTile(grid, grid.Comp, tile, new Tile(floor.TileId));
        }
    }

    #endregion

    #region Helpers

    /// <summary>helpers: после активации призраки могут становиться лайтгейстами.</summary>
    private void Helpers(Entity<AnomalousCrystalComponent> ent)
    {
        if (ent.Comp.ReadyToDeploy)
            return;

        ent.Comp.ReadyToDeploy = true;
        var role = EnsureComp<GhostRoleComponent>(ent);
        role.RoleName = Loc.GetString("anomalous-crystal-lightgeist-role");
        role.RoleDescription = Loc.GetString("anomalous-crystal-lightgeist-role-desc");
        role.RoleRules = Loc.GetString("ghost-role-component-default-rules");

        var spawner = EnsureComp<GhostRoleMobSpawnerComponent>(ent);
        spawner.Prototype = ent.Comp.Lightgeist;
        spawner.AvailableTakeovers = int.MaxValue;
        spawner.DeleteOnSpawn = false;

        _popup.PopupEntity(Loc.GetString("anomalous-crystal-helpers-ready"), ent, PopupType.Large);
    }

    #endregion

    #region Possessor

    /// <summary>is_valid_animal: живое, без игрока, мелкое и почти безобидное.</summary>
    private bool IsValidAnimal(EntityUid mob)
    {
        if (!_mobState.IsAlive(mob) || HasComp<HumanoidProfileComponent>(mob) || HasComp<MegafaunaAiComponent>(mob) ||
            HasComp<AnomalousPossessedComponent>(mob))
            return false;
        if (_mind.TryGetMind(mob, out _, out _))
            return false;
        if (!_thresholds.TryGetThresholdForState(mob, MobState.Dead, out var max) || max.Value > 100)
            return false;
        return !TryComp<MeleeWeaponComponent>(mob, out var melee) || melee.Damage.GetTotal() <= 5;
    }

    /// <summary>possessor: тело прячется в животном рядом, разум переходит в него.</summary>
    private void Possess(Entity<AnomalousCrystalComponent> ent, EntityUid user)
    {
        if (!HasComp<HumanoidProfileComponent>(user) || !_mind.TryGetMind(user, out var mindId, out var mind) ||
            !_ai.TryGetTile(ent, out var grid, out var center))
            return;

        var animals = new List<EntityUid>();
        foreach (var tile in MegafaunaAiSystem.RangeTiles(center, 1))
        {
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                if (IsValidAnimal(mob))
                    animals.Add(mob);
            }
        }

        if (animals.Count == 0)
        {
            _ai.SpawnAt(ent.Comp.Cockroach, grid, center + Vector2i.Down);
            return;
        }

        var animal = _random.Pick(animals);
        var container = _container.EnsureContainer<ContainerSlot>(animal, PossessorContainer);
        EnsureComp<AnomalousPossessorBodyComponent>(user);
        if (!_container.Insert(user, container))
        {
            RemComp<AnomalousPossessorBodyComponent>(user);
            return;
        }

        var possessed = EnsureComp<AnomalousPossessedComponent>(animal);
        possessed.Body = user;
        _mind.TransferTo(mindId, animal, mind: mind);
        possessed.Action = _actions.AddAction(animal, ent.Comp.ExitAction);
    }

    private void OnBodyDamage(Entity<AnomalousPossessorBodyComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }

    private void OnExitPossession(Entity<AnomalousPossessedComponent> ent, ref ExitPossessionActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        DumpContents(ent, false);
    }

    private void OnPossessedStateChanged(Entity<AnomalousPossessedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            DumpContents(ent, true);
    }

    /// <summary>closet/stasis/dump_contents: тело выходит, разум возвращается, животное взрывается.</summary>
    private void DumpContents(Entity<AnomalousPossessedComponent> ent, bool kill)
    {
        var body = ent.Comp.Body;
        if (TerminatingOrDeleted(body))
            return;

        RemComp<AnomalousPossessorBodyComponent>(body);
        if (_container.TryGetContainer(ent, PossessorContainer, out var container))
            _container.Remove(body, container);
        _transform.SetCoordinates(body, Transform(ent).Coordinates);

        if (_mind.TryGetMind(ent, out var mindId, out var mind))
            _mind.TransferTo(mindId, body, mind: mind);

        if (kill && TryComp<MobStateComponent>(body, out var bodyState))
            _mobState.ChangeMobState(body, MobState.Dead, bodyState);

        if (ent.Comp.Action is { } action)
            _actions.RemoveAction(action);

        RemComp<AnomalousPossessedComponent>(ent);
        _popup.PopupEntity(Loc.GetString("anomalous-crystal-possession-gib", ("animal", ent.Owner)), ent, PopupType.LargeCaution);
        QueueDel(ent);
    }

    #endregion
}
