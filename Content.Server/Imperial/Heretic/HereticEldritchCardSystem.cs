using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Doors.Components;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Server.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticEldritchCardSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedItemSystem _itemSystem = default!;
    [Dependency] private readonly ClothingSystem _clothingSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private const string PortalFixtureId = "eldritchPortalFixture";
    private const int MaxRandomTeleportAttempts = 20;
    private const float RandomTeleportRadius = 7f;
    private const int MaxStationTeleportAttempts = 30;

    public override void Initialize()
    {
        SubscribeLocalEvent<HereticEldritchCardComponent, AfterInteractEvent>(OnCardInteract);
        SubscribeLocalEvent<HereticEldritchPortalComponent, StartCollideEvent>(OnPortalCollide);
    }

    private void OnCardInteract(Entity<HereticEldritchCardComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled) return;
        if (!args.CanReach || args.Target == null) return;
        if (!HasComp<HereticComponent>(args.User)) return;

        // Copy identity, access and appearance from another ID card (except self)
        if (!HasComp<HereticEldritchCardComponent>(args.Target.Value)
            && TryComp<IdCardComponent>(args.Target.Value, out var sourceId)
            && TryComp<IdCardComponent>(ent.Owner, out var destId))
        {
            args.Handled = true;
            var target = args.Target.Value;

            _idCard.TryChangeFullName(ent.Owner, sourceId.FullName, destId);
            _idCard.TryChangeJobTitle(ent.Owner, sourceId.LocalizedJobTitle, destId);

            if (TryComp<AccessComponent>(target, out var sourceAccess)
                && TryComp<AccessComponent>(ent.Owner, out var destAccess))
            {
                destAccess.Tags.UnionWith(sourceAccess.Tags);
                Dirty(ent.Owner, destAccess);
            }

            if (TryComp<ItemComponent>(target, out var sourceItem)
                && TryComp<ItemComponent>(ent.Owner, out var destItem))
            {
                _itemSystem.CopyVisuals(ent.Owner, sourceItem, destItem);
            }

            if (TryComp<ClothingComponent>(target, out var sourceClothing)
                && TryComp<ClothingComponent>(ent.Owner, out var destClothing))
            {
                _clothingSystem.CopyVisuals(ent.Owner, sourceClothing, destClothing);
            }

            _popup.PopupEntity(Loc.GetString("heretic-eldritch-card-copy"), args.User, args.User, PopupType.Small);
            return;
        }

        if (!HasComp<AirlockComponent>(args.Target.Value)) return;

        args.Handled = true;

        var doorCoords = Transform(args.Target.Value).Coordinates;

        if (ent.Comp.PendingPortal == null || !Exists(ent.Comp.PendingPortal.Value))
        {
            // First door — spawn a portal and remember it
            var portal = Spawn("HereticEldritchPortal", doorCoords);
            var portalComp = EnsureComp<HereticEldritchPortalComponent>(portal);
            portalComp.Caster = args.User;
            ent.Comp.PendingPortal = portal;
        }
        else
        {
            // Second door — spawn second portal and link both
            var portal2 = Spawn("HereticEldritchPortal", doorCoords);
            var comp2 = EnsureComp<HereticEldritchPortalComponent>(portal2);
            comp2.Caster = args.User;
            comp2.LinkedPortal = ent.Comp.PendingPortal.Value;

            if (TryComp<HereticEldritchPortalComponent>(ent.Comp.PendingPortal.Value, out var comp1))
                comp1.LinkedPortal = portal2;

            ent.Comp.PendingPortal = null;
        }
    }

    private void OnPortalCollide(Entity<HereticEldritchPortalComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != PortalFixtureId) return;
        if (!args.OtherFixture.Hard) return;

        var subject = args.OtherEntity;

        // Don't teleport anchored entities
        if (Transform(subject).Anchored) return;

        var hasLinkedPortal = ent.Comp.LinkedPortal != null && Exists(ent.Comp.LinkedPortal.Value);

        if (HasComp<HereticComponent>(subject))
        {
            // Heretics step through to the linked gateway
            if (hasLinkedPortal)
                _transform.SetCoordinates(subject, Transform(ent.Comp.LinkedPortal!.Value).Coordinates);
            return;
        }

        // Non-heretics are flung to a random location on the station
        if (!TeleportToRandomStationLocation(subject, Transform(ent).Coordinates))
            TeleportRandomly(subject, Transform(ent).Coordinates);
    }

    private void TeleportRandomly(EntityUid subject, EntityCoordinates from)
    {
        var newCoords = from;
        for (var i = 0; i < MaxRandomTeleportAttempts; i++)
        {
            var candidate = from.Offset(_random.NextVector2(RandomTeleportRadius));
            if (!_lookup.AnyEntitiesIntersecting(_transform.ToMapCoordinates(candidate), LookupFlags.Static))
            {
                newCoords = candidate;
                break;
            }
        }
        _transform.SetCoordinates(subject, newCoords);
    }

    private bool TeleportToRandomStationLocation(EntityUid subject, EntityCoordinates from)
    {
        var gridUid = _transform.GetGrid(from);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return false;

        var aabb = grid.LocalAABB;
        if (aabb.IsEmpty())
            return false;

        for (var i = 0; i < MaxStationTeleportAttempts; i++)
        {
            var x = _random.NextFloat(aabb.Left, aabb.Right);
            var y = _random.NextFloat(aabb.Bottom, aabb.Top);
            var candidate = new EntityCoordinates(gridUid.Value, x, y);

            var tile = _map.GetTileRef(gridUid.Value, grid, candidate);
            if (tile.Tile.IsEmpty)
                continue;

            if (_lookup.AnyEntitiesIntersecting(_transform.ToMapCoordinates(candidate), LookupFlags.Static))
                continue;

            _transform.SetCoordinates(subject, candidate);
            return true;
        }

        return false;
    }
}
