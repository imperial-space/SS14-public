using Content.Server.Polymorph.Systems;
using Content.Shared.Imperial.Lavaland.BloodVial;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;

namespace Content.Server.Imperial.Lavaland.BloodVial;

public sealed class TreatedBloodVialSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TreatedBloodVialComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, TreatedBloodVialComponent component, UseInHandEvent args)
    {
        if (args.Handled || component.Used)
            return;

        if (!HasComp<MobStateComponent>(args.User))
            return;

        component.Used = true;
        _polymorph.PolymorphEntity(args.User, component.PolymorphTarget);
        QueueDel(uid);
        args.Handled = true;
    }
}
