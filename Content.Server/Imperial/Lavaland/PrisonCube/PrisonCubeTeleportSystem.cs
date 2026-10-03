using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.PrisonCube;

public sealed class PrisonCubeTeleportSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrisonCubeTeleportComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PrisonCubePairSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnSpawnerMapInit(EntityUid uid, PrisonCubePairSpawnerComponent comp, MapInitEvent args)
    {
        var coords = Transform(uid).Coordinates;
        var red = Spawn(comp.RedPrototype, coords);
        var blue = Spawn(comp.BluePrototype, coords);

        if (TryComp<PrisonCubeTeleportComponent>(red, out var redComp))
            redComp.Partner = blue;
        if (TryComp<PrisonCubeTeleportComponent>(blue, out var blueComp))
            blueComp.Partner = red;

        if (_container.TryGetContainingContainer(uid, out var container))
        {
            _container.Insert(red, container);
            _container.Insert(blue, container);
        }

        QueueDel(uid);
    }

    private void OnUseInHand(Entity<PrisonCubeTeleportComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryFindDestination(ent, out var destination))
        {
            _popup.PopupClient("Куб не находит свою пару.", args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var sourceCoords = Transform(ent).Coordinates;
        var destinationCoords = Transform(destination!.Value).Coordinates;

        Spawn(ent.Comp.SmokePrototype, sourceCoords);
        Spawn(ent.Comp.SmokePrototype, destinationCoords);

        _transform.SetCoordinates(args.User, destinationCoords);
        args.Handled = true;
    }

    private bool TryFindDestination(Entity<PrisonCubeTeleportComponent> ent, out EntityUid? destination)
    {
        destination = null;

        if (ent.Comp.Partner is { Valid: true } partner && Exists(partner))
        {
            destination = partner;
            return true;
        }

        return false;
    }
}
