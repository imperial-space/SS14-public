using Content.Shared.Imperial.LockDoor.Components;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Server.Doors.Systems;
using Content.Server.CustomDoorKey.Components;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Localization;
using Content.Shared.DoAfter;

namespace Content.Server.Imperial.LockDoor.Systems;

public sealed partial class LockDoorSystems : EntitySystem
{
    [Dependency] private readonly DoorSystem _doorSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedSkillsSystem _skillsSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LockDoorComponent, InteractUsingEvent>(OnClick);
        SubscribeLocalEvent<LockDoorComponent, LockDoorDoAfterEvent>(OnClickDoAfter);
    }

    public void OnClick(EntityUid uid, LockDoorComponent comp, InteractUsingEvent ev)
    {

        if (HasComp<SkillsComponent>(ev.User) && _skillsSystem.IntelligenceMin(ev.User))
        {
            _popupSystem.PopupEntity(Loc.GetString("lock-door-popup-low-intelligence"), ev.User, ev.User);
            return;
        }
        if (HasComp<SkillsComponent>(ev.User) && _skillsSystem.CanOpenDoorKey(ev.User))
        {

            var doAfterArgs = new DoAfterArgs(EntityManager, ev.User, 1.25f, new LockDoorDoAfterEvent(), uid, ev.Target, ev.Used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnDropItem = true
            };
            _doAfterSystem.TryStartDoAfter(doAfterArgs);
            return;
        }

        var has = false;
        if (!TryComp<KeyComponent>(ev.Used, out var accessUsedComponent) || !TryComp<DoorBoltComponent>(ev.Target, out var doorBoltComponent)) return;
        if (TryComp<DoorComponent>(ev.Target, out var door) && door.State != DoorState.Closed) return;

        var doorEntity = new Entity<DoorBoltComponent>(uid, doorBoltComponent);

        foreach (var i in accessUsedComponent.Accesses)
        {
            if (comp.AccessLists.Contains(i))
                has = true;
        }

        if (has)
        {
            bool isBolted = _doorSystem.IsBolted(ev.Target);
            _doorSystem.TrySetBoltDown(doorEntity, !isBolted);

            // Show popup
            if (isBolted)
            {
                _popupSystem.PopupEntity(Loc.GetString("lock-door-popup-unlock"), ev.Target, ev.User);

            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("lock-door-popup-lock"), ev.Target, ev.User);
            }
            if (TryComp<DoorHackableComponent>(ev.Target, out var hack))
            {
                hack.LockPickProgress = 0;
            }
        }
    }

    private void OnClickDoAfter(EntityUid uid, LockDoorComponent comp, LockDoorDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Used is not { } used || args.Target is not { } target)
            return;

        var has = false;
        if (!TryComp<KeyComponent>(used, out var accessUsedComponent) ||
            !TryComp<DoorBoltComponent>(target, out var doorBoltComponent))
            return;

        if (TryComp<DoorComponent>(target, out var door) && door.State != DoorState.Closed)
            return;

        var doorEntity = new Entity<DoorBoltComponent>(target, doorBoltComponent);

        foreach (var i in accessUsedComponent.Accesses)
        {
            if (comp.AccessLists.Contains(i))
                has = true;
        }

        if (has)
        {
            bool isBolted = _doorSystem.IsBolted(target);
            _doorSystem.TrySetBoltDown(doorEntity, !isBolted);

            // Show popup
            if (isBolted)
            {
                _popupSystem.PopupEntity(Loc.GetString("lock-door-popup-unlock"), target, args.User);
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("lock-door-popup-lock"), target, args.User);
            }

            if (TryComp<DoorHackableComponent>(target, out var hack))
            {
                hack.LockPickProgress = 0;
            }
        }

        args.Handled = true;
    }
}
