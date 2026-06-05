using Content.Shared.Mobs;

namespace Content.Server.Imperial.Lavaland.Megafauna;

public sealed class MegafaunaVanishOnDeathSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegafaunaVanishOnDeathComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MegafaunaVanishOnDeathComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsDying)
                continue;

            comp.TimeRemaining -= frameTime;
            if (comp.TimeRemaining <= 0f)
                QueueDel(uid);
        }
    }

    private void OnMobStateChanged(Entity<MegafaunaVanishOnDeathComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (ent.Comp.Delay <= 0f)
        {
            QueueDel(ent);
            return;
        }

        ent.Comp.IsDying = true;
        ent.Comp.TimeRemaining = ent.Comp.Delay;
    }
}
