using System.Linq;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.TerrorSpider.Systems;

public abstract class SharedTerrorSpiderCocoonSystem : EntitySystem
{
    private const string CocoonContainerId = "cocoon-body";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderCocoonComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderCocoonComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderCocoonComponent, TerrorSpiderCocoonActionEvent>(OnCocoonAction);
        SubscribeLocalEvent<TerrorSpiderCocoonComponent, TerrorSpiderCocoonDoAfterEvent>(OnCocoonDoAfter);
        SubscribeLocalEvent<TerrorSpiderCocoonPrisonComponent, InteractUsingEvent>(OnCocoonInteractUsing);
    }

    private void OnCocoonInteractUsing(Entity<TerrorSpiderCocoonPrisonComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_net.IsClient)
            return;

        var container = _container.EnsureContainer<Container>(ent.Owner, CocoonContainerId);

        if (container.ContainedEntities.Count == 0)
            return;

        if (!HasComp<SharpComponent>(args.Used))
        {
            var message = Loc.GetString("butcherable-need-knife", ("target", ent.Owner));
            _popup.PopupEntity(message, ent.Owner, args.User);
            return;
        }

        foreach (var contained in container.ContainedEntities.ToArray())
        {
            _container.Remove(contained, container);
        }

        Del(ent.Owner);
        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderCocoonComponent comp, MapInitEvent args)
    {
        if (!comp.Enabled)
            return;

        _actions.AddAction(uid, ref comp.ActionEntity, comp.Action);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderCocoonComponent comp, ComponentShutdown args)
    {
        if (!comp.Enabled)
            return;

        _actions.RemoveAction(uid, comp.ActionEntity);
    }

    private void OnCocoonAction(Entity<TerrorSpiderCocoonComponent> ent, ref TerrorSpiderCocoonActionEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        if (args.Handled)
            return;

        var target = args.Target;
        if (target == EntityUid.Invalid)
            return;

        if (target == ent.Owner)
            return;

        if (!TryComp<MobStateComponent>(target, out var mobState)
            || mobState.CurrentState != MobState.Alive)
        {
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager,
            ent.Owner,
            ent.Comp.CocoonDelay,
            new TerrorSpiderCocoonDoAfterEvent(),
            ent.Owner,
            target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
    }

    private void OnCocoonDoAfter(Entity<TerrorSpiderCocoonComponent> ent, ref TerrorSpiderCocoonDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not EntityUid target || target == EntityUid.Invalid || Deleted(target) || target == ent.Owner)
            return;

        if (!HasComp<MobStateComponent>(target))
            return;

        if (_net.IsClient)
            return;

        var cocoon = Spawn(ent.Comp.CocoonPrototype, Transform(target).Coordinates);
        var container = _container.EnsureContainer<Container>(cocoon, CocoonContainerId);

        if (!_container.Insert(target, container))
        {
            Del(cocoon);
            return;
        }

        RaiseLocalEvent(ent.Owner, new TerrorSpiderCocoonWrappedEvent(ent.Owner, target, cocoon));

        args.Handled = true;
    }
}
