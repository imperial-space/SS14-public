using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>/obj/effect/wisp_mobile: огонёк, на котором похищенный катается по пустоте.</summary>
[RegisterComponent]
public sealed partial class ImperialWispComponent : Component
{
    [ViewVariables]
    public EntityUid? Driver;

    [ViewVariables]
    public TimeSpan ReleaseAt;

    /// <summary>heal_per_food.</summary>
    [DataField]
    public float HealPerFood = 15f;
}

/// <summary>/obj/effect/wisp_food.</summary>
[RegisterComponent]
public sealed partial class ImperialWispFoodComponent : Component;

/// <summary>Похищенный за рулём огонька: неуязвим, руки заняты.</summary>
[RegisterComponent]
public sealed partial class ImperialWispDriverComponent : Component;

/// <summary>
/// voidwalker_void: чёрная комната 32×32, куда попадают похищенные. Огонёк ест еду и лечит водителя,
/// через 60 секунд водителя выбрасывает в безопасное место станции.
/// </summary>
public sealed class ImperialVoidSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly Content.Shared.Body.Systems.SharedBloodstreamSystem _blood = default!;

    private const int Size = 32;
    private const string DriverContainer = "imperial_wisp_driver";
    private static readonly TimeSpan RideTime = TimeSpan.FromSeconds(60);

    private EntityUid? _voidGrid;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialWispDriverComponent, BeforeDamageChangedEvent>((_, _, args) =>
        {
            // TRAIT_STASIS: урон не проходит, лечение — проходит.
            if (args.Damage.AnyPositive())
                args.Cancelled = true;
        });
        SubscribeLocalEvent<ImperialWispDriverComponent, InteractionAttemptEvent>((_, _, args) => args.Cancelled = true);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _voidGrid = null);
    }

    /// <summary>lazy_load_template(VOIDWALKER_VOID).</summary>
    private EntityUid GetVoid()
    {
        if (_voidGrid is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, Loc.GetString("imperial-voidwalker-void-name"));
        _map.SetAmbientLight(mapId, Color.Black);
        var grid = _mapManager.CreateGridEntity(mapId);
        _meta.SetEntityName(grid, Loc.GetString("imperial-voidwalker-void-name"));
        var tile = new Tile(_tileDefs["FloorDark"].TileId);
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles.Add((new Vector2i(x, y), tile));
            }
        }

        _map.SetTiles(grid.Owner, grid.Comp, tiles);
        // В пустоте дышится: обычный воздух на всей карте, без своей атмосферы грида.
        var air = new Content.Shared.Atmos.GasMixture(Content.Shared.Atmos.Atmospherics.CellVolume) { Temperature = Content.Shared.Atmos.Atmospherics.T20C };
        air.AdjustMoles(Content.Shared.Atmos.Gas.Oxygen, Content.Shared.Atmos.Atmospherics.OxygenMolesStandard);
        air.AdjustMoles(Content.Shared.Atmos.Gas.Nitrogen, Content.Shared.Atmos.Atmospherics.NitrogenMolesStandard);
        _atmos.SetMapAtmosphere(mapUid, false, air);
        RemComp<Content.Shared.Atmos.Components.GridAtmosphereComponent>(grid.Owner);
        Spawn("ImperialWispFood", RandomTile(grid.Owner));
        _voidGrid = grid.Owner;
        return grid.Owner;
    }

    private EntityCoordinates RandomTile(EntityUid grid)
    {
        return new EntityCoordinates(grid, _random.Next(1, Size - 1) + 0.5f, _random.Next(1, Size - 1) + 0.5f);
    }

    /// <summary>take_them(): исцелить и посадить на огонёк в пустоте.</summary>
    public void SendToVoid(EntityUid victim)
    {
        // heal_and_revive(90) + adjust_oxy_loss(-100)
        HealOrdered(victim, 90f);
        // TRAIT_STASIS: кровь не уходит, пока огонёк везёт.
        _blood.TryModifyBleedAmount(victim, -100f);
        _blood.TryModifyBloodLevel(victim, 100);
        _damage.TryChangeDamage(victim, new DamageSpecifier { DamageDict = { ["Asphyxiation"] = -100 } }, true);
        var grid = GetVoid();
        var wisp = Spawn("ImperialWispMobile", RandomTile(grid));
        var comp = EnsureComp<ImperialWispComponent>(wisp);
        comp.Driver = victim;
        comp.ReleaseAt = _timing.CurTime + RideTime;

        var container = _containers.EnsureContainer<ContainerSlot>(wisp, DriverContainer);
        _containers.Insert(victim, container, force: true);
        EnsureComp<ImperialWispDriverComponent>(victim);
        _mover.SetRelay(victim, wisp);
        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-void-arrive"), victim, victim, PopupType.LargeCaution);
    }

    /// <summary>get_safe_random_station_turf_equal_weight(): клетка станции с нормальным воздухом.</summary>
    private EntityCoordinates? SafeStationTile()
    {
        var stations = _station.GetStations();
        if (stations.Count == 0 || _station.GetLargestGrid(_random.Pick(stations)) is not { } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var tiles = _map.GetAllTiles(gridUid, grid).ToList();
        for (var i = 0; i < 300 && tiles.Count > 0; i++)
        {
            var tile = _random.Pick(tiles);
            if (_turf.IsSpace(tile) || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                continue;

            var air = _atmos.GetTileMixture(gridUid, null, tile.GridIndices);
            if (air == null || air.Pressure < 80f || air.Pressure > 130f || air.Temperature < 270f || air.Temperature > 330f)
                continue;

            return _map.GridTileToLocal(gridUid, grid, tile.GridIndices);
        }

        return null;
    }

    /// <summary>wisp_mobile/Exited(): «Вы чувствуете, что за вами наблюдают».</summary>
    private void Release(EntityUid wisp, ImperialWispComponent comp)
    {
        if (comp.Driver is { } driver && !TerminatingOrDeleted(driver))
        {
            RemComp<RelayInputMoverComponent>(driver);
            RemComp<ImperialWispDriverComponent>(driver);
            _containers.TryRemoveFromContainer(driver, true);
            if (SafeStationTile() is { } dest)
                _transform.SetCoordinates(driver, dest);
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-void-watched"), driver, driver, PopupType.LargeCaution);
        }

        QueueDel(wisp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialWispComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var wisp, out var xform))
        {
            if (now >= wisp.ReleaseAt || wisp.Driver is not { } driver || TerminatingOrDeleted(driver))
            {
                Release(uid, wisp);
                continue;
            }

            // maybe_loop_us(): у края комнаты огонёк выныривает с другой стороны.
            var pos = xform.LocalPosition;
            var wrapped = new Vector2(Wrap(pos.X), Wrap(pos.Y));
            if (wrapped != pos)
                _transform.SetLocalPosition(uid, wrapped, xform);

            // Moved(): съесть еду, подлечить водителя, вырастить новую.
            foreach (var food in EntityManager.System<EntityLookupSystem>().GetEntitiesInRange<ImperialWispFoodComponent>(xform.Coordinates, 0.6f))
            {
                var foodXform = Transform(food);
                QueueDel(food);
                if (foodXform.GridUid is { } grid)
                    Spawn("ImperialWispFood", RandomTile(grid));

                HealOrdered(driver, wisp.HealPerFood);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/server-ready.ogg"), uid);
            }
        }
    }

    private static float Wrap(float v)
    {
        if (v < 0.5f)
            return Size - 1.5f;
        if (v > Size - 0.5f)
            return 1.5f;
        return v;
    }

    /// <summary>heal_ordered_damage(15, BRUTE, BURN, OXY).</summary>
    private void HealOrdered(EntityUid uid, float amount)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var all = _damage.GetAllDamage((uid, damageable));
        var heal = new DamageSpecifier();
        foreach (var type in new[] { "Blunt", "Slash", "Piercing", "Heat", "Cold", "Shock", "Caustic", "Asphyxiation" })
        {
            if (amount <= 0)
                break;

            if (!all.DamageDict.TryGetValue(type, out var have) || have <= 0)
                continue;

            var take = Math.Min(amount, have.Float());
            heal.DamageDict[type] = -take;
            amount -= take;
        }

        _damage.TryChangeDamage(uid, heal, true);
    }
}
