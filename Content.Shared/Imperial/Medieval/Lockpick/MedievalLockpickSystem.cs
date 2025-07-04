using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Shared.MedievalLockpick.Components;
using Robust.Shared.Player;
using Content.Shared.DoAfter;
using Robust.Shared.Random;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.MedievalLockpickSystem;

public sealed class MedievalLockpickSystem : EntitySystem
{
    public const float DefaultChance = 0.2f;

    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalLockpickComponent, BeforeRangedInteractEvent>(OnUseInHand);
        SubscribeLocalEvent<MedievalLockpickComponent, MedievalLockpickDoAfterEvent>(OnDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

    }

    public void OnUseInHand(EntityUid uid, MedievalLockpickComponent comp, BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;
        OnUse(args.Target, args.User, comp);
    }

    public void OnUse(EntityUid? target, EntityUid user, MedievalLockpickComponent comp)
    {
        if (target == null)
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, user, 1f, new MedievalLockpickDoAfterEvent(), target.Value, target: target.Value, used: comp.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfterSystem.TryStartDoAfter(doAfterEventArgs);

    }

    private void OnDoAfter(EntityUid uid, MedievalLockpickComponent component, MedievalLockpickDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !_timing.IsFirstTimePredicted)
            return;

        if (args.Args.Target is not { Valid: true } door)
            return;

        if (!TryComp<DoorComponent>(door, out var doorcomp))
            return;

        var ev = new GetLockpickChanceModifiersEvent(1f);
        RaiseLocalEvent(args.Args.User, ref ev);

        if (!_random.Prob(DefaultChance * ev.Modifier))
        {
            _popupSystem.PopupEntity("Взлом неудачный, попробуйте еще раз", args.Args.User, PopupType.LargeCaution);
            return;
        }

        _popupSystem.PopupEntity("Взлом успешный", args.Args.User, PopupType.LargeCaution);

        switch (doorcomp.State)
        {
            case DoorState.Open:
                _door.StartClosing(door, doorcomp, args.Args.User, false);
                break;
            case DoorState.Closed:
                _door.StartOpening(door, doorcomp, args.Args.User, false);
                break;
            default:
                break;
        }

        args.Handled = true;
    }
}
