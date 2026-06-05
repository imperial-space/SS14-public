using Content.Shared.Mobs;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.BossLoot;

/// <summary>
/// Обрабатывает дроп лута для сущностей с <see cref="BossLootComponent"/> при смерти.
/// </summary>
public sealed class BossLootSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossLootComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<BossLootComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (ent.Comp.LootDropped)
            return;

        ent.Comp.LootDropped = true;

        var coords = Transform(ent).Coordinates;

        foreach (var entry in ent.Comp.Loot)
        {
            if (entry.Chance < 1.0f && !_random.Prob(entry.Chance))
                continue;

            var count = entry.MinCount == entry.MaxCount
                ? entry.MinCount
                : _random.Next(entry.MinCount, entry.MaxCount + 1);

            for (var i = 0; i < count; i++)
                Spawn(entry.Prototype, coords);
        }
    }
}
