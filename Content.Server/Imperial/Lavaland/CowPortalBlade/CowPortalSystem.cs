using System.Numerics;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Lavaland.CowPortalBlade;

/// <summary>
/// Управляет порталом: после задержки спавнит коров вокруг портала и сам исчезает.
/// </summary>
public sealed class CowPortalSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CowPortalComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CowPortalComponent> ent, ref MapInitEvent args)
    {
        var uid = ent.Owner;
        var comp = ent.Comp;

        Timer.Spawn(comp.SpawnDelayMs, () =>
        {
            if (!Exists(uid) || TerminatingOrDeleted(uid))
                return;

            var coords = Transform(uid).Coordinates;

            for (var i = 0; i < comp.CowCount; i++)
            {
                var offset = new Vector2(
                    _random.NextFloat(-comp.SpawnRadius, comp.SpawnRadius),
                    _random.NextFloat(-comp.SpawnRadius, comp.SpawnRadius));

                SpawnAtPosition(comp.CowPrototype, coords.Offset(offset));
            }
        });
    }
}
