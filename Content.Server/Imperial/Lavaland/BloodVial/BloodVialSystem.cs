using Content.Server.Actions;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Imperial.Lavaland.BloodVial;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.BloodVial;

public sealed class BloodVialSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;

    private const string RevertActionId = "ActionRevertPolymorph";
    private const string PolymorphDragonActionId = "ActionPolymorphLesserDragon";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodVialComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, BloodVialComponent component, UseInHandEvent args)
    {
        if (args.Handled || component.Used)
            return;

        // Only works on alive mobs
        if (!HasComp<MobStateComponent>(args.User))
            return;

        component.Used = true;

        var isLesserDragon = _random.Prob(0.5f);
        var protoId = isLesserDragon ? component.PolymorphB : component.PolymorphA;
        var newEntity = _polymorph.PolymorphEntity(args.User, protoId);

        if (isLesserDragon && newEntity.HasValue)
        {
            // Dragon form gets a revert action (forced:true polymorphs don't get it automatically)
            EntityUid? revertAction = null;
            _actions.AddAction(newEntity.Value, ref revertAction, RevertActionId);

            // Original human gets a "transform back to dragon" action (survives in PausedMap)
            EnsureComp<PolymorphableComponent>(args.User);
            EntityUid? dragonAction = null;
            _actions.AddAction(args.User, ref dragonAction, PolymorphDragonActionId);
        }

        // Delete the vial after use
        QueueDel(uid);
        args.Handled = true;
    }
}
