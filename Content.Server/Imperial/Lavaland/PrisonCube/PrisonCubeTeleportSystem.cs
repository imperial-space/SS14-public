using Content.Shared.Interaction.Events;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Lavaland.PrisonCube;

public sealed class PrisonCubeTeleportSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrisonCubeTeleportComponent, UseInHandEvent>(OnUseInHand);
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

        var query = EntityQueryEnumerator<PrisonCubeTeleportComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid == ent.Owner)
                continue;

            if (comp.LinkChannel != ent.Comp.TargetChannel)
                continue;

            destination = uid;
            break;
        }

        return destination != null;
    }
}
