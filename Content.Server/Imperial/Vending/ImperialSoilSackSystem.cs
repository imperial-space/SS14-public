using Content.Server.Botany.Components;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Vending;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Vending;

/// <summary>soil_sack/interact_with_atom(): секунда — и на открытом полу появляется грядка, мешок тратится.</summary>
public sealed class ImperialSoilSackSystem : EntitySystem
{
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialSoilSackComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ImperialSoilSackComponent, ImperialSoilSackDoAfterEvent>(OnDoAfter);
    }

    private bool CanPlace(EntityUid user, Robust.Shared.Map.EntityCoordinates coords, out Robust.Shared.Map.EntityCoordinates center)
    {
        center = coords;
        if (_turf.GetTileRef(coords) is not { } tile || _turf.IsSpace(tile) || !TryComp<MapGridComponent>(tile.GridUid, out var grid))
            return false;

        foreach (var ent in _lookup.GetLocalEntitiesIntersecting(tile.GridUid, tile.GridIndices))
        {
            if (!HasComp<PlantHolderComponent>(ent))
                continue;

            _popup.PopupEntity(Loc.GetString("imperial-soil-sack-occupied"), user, user);
            return false;
        }

        center = _map.GridTileToLocal(tile.GridUid, grid, tile.GridIndices);
        return true;
    }

    private void OnAfterInteract(Entity<ImperialSoilSackComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != null && !HasComp<MapGridComponent>(args.Target))
            return;

        if (!CanPlace(args.User, args.ClickLocation, out var center))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new ImperialSoilSackDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        ent.Comp.Target = GetNetCoordinates(center);
    }

    private void OnDoAfter(Entity<ImperialSoilSackComponent> ent, ref ImperialSoilSackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Target is not { } target)
            return;

        var coords = GetCoordinates(target);
        if (!CanPlace(args.User, coords, out var center))
            return;

        args.Handled = true;
        Spawn(ent.Comp.Soil, center);
        _audio.PlayPvs(ent.Comp.Sound, center);
        QueueDel(ent);
    }
}
