using System.Numerics;
using Content.Server.DeviceLinking.Components;
using Content.Server.DeviceNetwork;
using Content.Server.DoAfter;
using Content.Server.Imperial.DeviceLinking;
using Content.Shared.DeviceLinking.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeviceLinking.Systems;

public sealed class SignalSwitchImperialSystem : EntitySystem
{
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SignalSwitchImperialComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SignalSwitchImperialComponent, ActivateInWorldEvent>(OnActivated);
        SubscribeLocalEvent<SignalSwitchImperialComponent, OnDoAfterSignalSwitchEvent>(OnDoAfter);
    }
    private void OnInit(EntityUid uid, SignalSwitchImperialComponent comp, ComponentInit args)
    {
        _deviceLink.EnsureSourcePorts(uid, comp.OnPort, comp.OffPort, comp.StatusPort);
    }

    private void OnActivated(EntityUid uid, SignalSwitchImperialComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || args.Target == null || HasComp<SignalSwitchImperialHelpComponent>(args.User))
            return;

        var ev = new CheckSignalSwitchActivationTimeEvent(1f);
        RaiseLocalEvent(args.User, ref ev);

        var sdoAfter = new DoAfterArgs(EntityManager, args.User, comp.Timing * ev.Modifier, new OnDoAfterSignalSwitchEvent(), args.Target, target: args.User)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false, // stuns itself
        };

        if (!_doAfter.TryStartDoAfter(sdoAfter))
            return;

        _audio.PlayPvs(comp.ClickSound, uid, AudioParams.Default.WithVariation(0.125f).WithVolume(8f));
        EnsureComp<SignalSwitchImperialHelpComponent>(args.User);
        args.Handled = true;
    }
    private void OnDoAfter(EntityUid uid, SignalSwitchImperialComponent comp, OnDoAfterSignalSwitchEvent ev)
    {
        RemComp<SignalSwitchImperialHelpComponent>(ev.User);

        if (ev.Cancelled || ev.Target == null) return;


        comp.State = !comp.State;
        _deviceLink.InvokePort(uid, comp.State ? comp.OnPort : comp.OffPort);
        _deviceLink.SendSignal(uid, comp.StatusPort, comp.State);
    }
}
