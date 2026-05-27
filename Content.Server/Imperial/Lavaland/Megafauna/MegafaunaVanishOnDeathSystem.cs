using Content.Shared.Mobs;

namespace Content.Server.Imperial.Lavaland.Megafauna;

public sealed class MegafaunaVanishOnDeathSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegafaunaVanishOnDeathComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<MegafaunaVanishOnDeathComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        QueueDel(ent);
    }
}
