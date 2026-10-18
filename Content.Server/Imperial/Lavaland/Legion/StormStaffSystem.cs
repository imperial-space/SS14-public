using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Lavaland.StormStaff;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Lavaland.Legion;

/// <summary>Перенос obj/item/storm_staff: attack_self (рассеять бурю) и thunder_blast.</summary>
public sealed class StormStaffSystem : EntitySystem
{
    [Dependency] private readonly MegafaunaAiSystem _ai = default!;
    [Dependency] private readonly LavalandStormSystem _storm = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly Vector2i[] AllDirs =
    {
        new(0, 1), new(1, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StormStaffComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<StormStaffComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<StormStaffComponent, StormStaffDispelDoAfterEvent>(OnDispel);
        SubscribeLocalEvent<StormStaffComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnExamined(Entity<StormStaffComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("storm-staff-examine", ("charges", ent.Comp.Charges)));
    }

    private bool TryGetStorm(EntityUid uid, out EntityUid mapUid, out LavalandMapComponent storm)
    {
        mapUid = default;
        storm = default!;
        if (Transform(uid).MapUid is not { } map || !TryComp(map, out LavalandMapComponent? comp))
            return false;
        mapUid = map;
        storm = comp;
        return true;
    }

    /// <summary>attack_self: держит посох к небу 3 с и рассеивает бурю.</summary>
    private void OnUseInHand(Entity<StormStaffComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryGetStorm(args.User, out _, out var storm) || storm.StormState == LavalandStormState.Idle)
            return;

        args.Handled = true;
        if (storm.StormState == LavalandStormState.Ending)
        {
            _popup.PopupEntity(Loc.GetString("storm-staff-already-ending"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("storm-staff-hold-up"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DispelTime, new StormStaffDispelDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnDispel(Entity<StormStaffComponent> ent, ref StormStaffDispelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryGetStorm(args.User, out var map, out var storm))
            return;

        args.Handled = true;
        if (!_storm.TryWindDown(map, storm))
            return;

        _popup.PopupEntity(Loc.GetString("storm-staff-dispel", ("user", args.User)), args.User, PopupType.Large);
        _ai.PlaySound(ent.Comp.DispelSound, Transform(args.User).Coordinates, 8f);
    }

    /// <summary>thunder_blast: через 1.5 с молния в клетку, в бурю — 3x3 и вдвое сильнее.</summary>
    private void OnAfterInteract(Entity<StormStaffComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("storm-staff-needs-charge"), args.User, args.User);
            return;
        }

        if (!_ai.TryGetTile(args.ClickLocation, out var grid, out var tile))
        {
            _popup.PopupEntity(Loc.GetString("storm-staff-cant-bolt"), args.User, args.User);
            return;
        }

        if (!ent.Comp.Targeted.Add((grid.Owner, tile)))
        {
            _popup.PopupEntity(Loc.GetString("storm-staff-already-targeted"), args.User, args.User);
            return;
        }

        var boosted = TryGetStorm(grid.Owner, out _, out var storm) &&
                      storm.StormState == LavalandStormState.Active &&
                      HasComp<BiomeComponent>(grid.Owner);

        _ai.PlaySound(ent.Comp.AimSound, Transform(ent).Coordinates, -10f);
        _popup.PopupEntity(Loc.GetString("storm-staff-aim"), args.User, args.User);
        _ai.SpawnAt(ent.Comp.Telegraph, grid, tile);
        _ai.Schedule(null, ent.Comp.BoltDelay, () => Thunderbolt(ent, grid, tile, boosted));

        ent.Comp.Charges--;
        _ai.Schedule(null, ent.Comp.ChargeTime, () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            ent.Comp.Charges = Math.Min(ent.Comp.Charges + 1, ent.Comp.MaxCharges);
            _ai.PlaySound(ent.Comp.RechargeSound, Transform(ent).Coordinates, -10f);
        });
    }

    private void Thunderbolt(Entity<StormStaffComponent> ent, Entity<MapGridComponent> grid, Vector2i target, bool boosted)
    {
        if (!TerminatingOrDeleted(ent))
            ent.Comp.Targeted.Remove((grid.Owner, target));

        _ai.SpawnAt(ent.Comp.Thunderbolt, grid, target);
        var tiles = new List<Vector2i> { target };
        if (boosted)
        {
            foreach (var dir in AllDirs)
                tiles.Add(target + dir);
        }

        foreach (var tile in tiles)
        {
            _ai.SpawnAt(ent.Comp.Electricity, grid, tile);
            foreach (var mob in _ai.MobsOnTile(grid, tile))
            {
                _popup.PopupEntity(Loc.GetString("storm-staff-struck"), mob, mob, PopupType.LargeCaution);
                var damage = ent.Comp.BoltDamage
                             * (HasComp<HumanoidProfileComponent>(mob) ? 1 : 3)
                             * (tile == target ? 2 : 1)
                             * (boosted ? 2 : 1);
                _ai.Damage(mob, "Shock", damage, ent);
            }
        }

        // explosion(flame_range = boosted ? 2 : 1)
        foreach (var tile in MegafaunaAiSystem.RangeTiles(target, boosted ? 2 : 1))
        {
            foreach (var mob in _ai.MobsOnTile(grid, tile))
                _ai.Ignite(mob);
        }

        var coords = _ai.TileCenter(grid, target);
        _ai.PlaySound(ent.Comp.BoltSound, coords, 5f);
        _popup.PopupCoordinates(Loc.GetString("storm-staff-strikes"), coords, PopupType.MediumCaution);
    }
}
