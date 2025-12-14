using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DoAfter;
using Content.Shared.Atmos.Components;
using Content.Shared.DoAfter;
using Content.Shared.IgnitionSource;
using Content.Shared.Imperial.Medieval.Igniter;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Igniter;


public sealed partial class IgniterSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly FlammableSystem _flammableSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IgniterComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<IgniterComponent, IgniteAttemptDoAfterEvent>(OnIgniteAttemptEnded);
    }

    private void OnInteract(EntityUid uid, IgniterComponent component, AfterInteractEvent args)
    {
        if (!HasComp<FlammableComponent>(args.Target)) return;

        var ev = new IgniteAttemptDoAfterEvent(GetNetEntity(args.User), GetNetEntity(args.Target.Value));
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.IgniteTime, ev, uid, target: args.Target, used: args.User)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            MovementThreshold = 0.5f,
            CancelDuplicate = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    private void OnIgniteAttemptEnded(EntityUid uid, IgniterComponent component, ref IgniteAttemptDoAfterEvent args)
    {
        if (args.Cancelled) return;

        var performer = GetEntity(args.Performer);
        var target = GetEntity(args.IgniteTarget);

        SpawnAtPosition(component.EffectPrototype, Transform(performer).Coordinates);
        _audioSystem.PlayPvs(component.IgniteSound, performer);

        args.Repeat = !TryIgniteTarget(performer, target, component);
    }

    #region Helpers

    private bool TryIgniteTarget(EntityUid performer, EntityUid target, IgniterComponent igniterComponent)
    {
        if (!HasComp<FlammableComponent>(target)) return false;
        if (!_random.Prob(igniterComponent.IgniteChance)) return false;

        _flammableSystem.AdjustFireStacks(target, igniterComponent.FlammableStacks);
        _flammableSystem.Ignite(target, performer);

        RaiseLocalEvent(target, new IgniteEvent());

        return true;
    }

    #endregion
}
