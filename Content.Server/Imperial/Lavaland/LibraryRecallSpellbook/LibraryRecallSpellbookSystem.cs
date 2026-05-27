using System.Linq;
using Content.Server.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.ItemRecall;

namespace Content.Server.Imperial.Lavaland.LibraryRecallSpellbook;

public sealed class LibraryRecallSpellbookSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LibraryRecallSpellbookComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<LibraryRecallSpellbookComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (HasRecallAction(args.User))
        {
            args.Handled = true;
            return;
        }

        EntityUid? actionEnt = null;
        _actions.AddAction(args.User, ref actionEnt, ent.Comp.ActionProto);
        args.Handled = true;
    }

    private bool HasRecallAction(EntityUid user)
    {
        if (!TryComp<ActionsComponent>(user, out var actions))
            return false;

        return actions.Actions.Any(HasComp<ItemRecallComponent>);
    }
}
