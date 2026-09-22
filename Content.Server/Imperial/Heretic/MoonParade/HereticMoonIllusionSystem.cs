using System.Numerics;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic.MoonParade;

public sealed class HereticMoonIllusionSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom        _random  = default!;
    [Dependency] private readonly SharedTransformSystem _xform   = default!;
    [Dependency] private readonly SharedPhysicsSystem   _physics = default!;

    private const float MoveSpeed = 1.5f;

    private static readonly Direction[] Directions =
        { Direction.South, Direction.North, Direction.East, Direction.West,
          Direction.SouthEast, Direction.SouthWest, Direction.NorthEast, Direction.NorthWest };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonIllusionComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, HereticMoonIllusionComponent comp, MapInitEvent args)
    {
        var dir = _random.Pick(Directions);
        _xform.SetLocalRotation(uid, dir.ToAngle());

        if (!TryComp<PhysicsComponent>(uid, out var physics)) return;
        var vec = dir.ToVec() * MoveSpeed;
        _physics.SetLinearVelocity(uid, vec, body: physics);
    }
}
