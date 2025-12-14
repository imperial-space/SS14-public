using System.Numerics;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class SpawnAtPosition : BossAttackAction
{
    [DataField(required: true)]
    public string[] Prototypes;

    [DataField]
    public int RandomDistance = 0;

    [DataField]
    public int SpawnCount = 0;

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var map = entMan.System<MapSystem>();

        foreach (var target in targets)
        {
            var xform = entMan.GetComponent<TransformComponent>(target);
            for (var i = 0; i < SpawnCount; i++)
            {
                var targetCoords = xform.Coordinates;

                if (RandomDistance != 0)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        var result = new EntityCoordinates(xform.Coordinates.EntityId, targetCoords.Position + random.NextVector2(-RandomDistance, RandomDistance));
                        if (xform.GridUid.HasValue && map.AnchoredEntityCount(xform.GridUid.Value, entMan.GetComponent<MapGridComponent>(xform.GridUid.Value), result.Position.Floored()) == 0)
                        {
                            targetCoords = result;
                            break;
                        }
                    }
                }

                var spike = entMan.SpawnEntity(random.Pick(Prototypes), targetCoords);
                var comp = entMan.EnsureComponent<BossAttackComponent>(spike);
                comp.Boss = boss;
            }
        }
    }
}
