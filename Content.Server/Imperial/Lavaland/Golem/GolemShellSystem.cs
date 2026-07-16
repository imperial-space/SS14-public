using Content.Server.Popups;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.Lavaland.Golem;

public sealed class GolemShellSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GolemShellComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, GolemShellComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var itemProto = MetaData(args.Used).EntityPrototype?.ID;
        if (itemProto == null || !comp.MaterialToGolem.ContainsKey(itemProto))
            return;

        if (comp.CurrentMaterial != null && comp.CurrentMaterial != itemProto)
        {
            _popup.PopupEntity(_loc.GetString("golem-shell-wrong-material"), uid, args.User);
            args.Handled = true;
            return;
        }

        int toAdd;
        if (TryComp<StackComponent>(args.Used, out var stack))
        {
            var needed = comp.RequiredCount - comp.MaterialCount;
            toAdd = Math.Min(stack.Count, needed);
            _stacks.ReduceCount((args.Used, stack), toAdd);
        }
        else
        {
            toAdd = 1;
            QueueDel(args.Used);
        }

        comp.CurrentMaterial = itemProto;
        comp.MaterialCount += toAdd;

        var remaining = comp.RequiredCount - comp.MaterialCount;
        if (remaining > 0)
        {
            _popup.PopupEntity(_loc.GetString("golem-shell-material-added", ("remaining", remaining)), uid, args.User);
            args.Handled = true;
            return;
        }

        var golemProto = comp.MaterialToGolem[comp.CurrentMaterial!];
        Spawn(golemProto, Transform(uid).Coordinates);
        _popup.PopupEntity(_loc.GetString("golem-shell-complete"), uid, PopupType.LargeCaution);
        QueueDel(uid);
        args.Handled = true;
    }
}
