using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Repairing;
using Content.Shared.Imperial.Medieval.Ships.WaterPump;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.WaterPump;

public sealed class ServerWaterPumpSystem : EntitySystem
{
    [Dependency] private readonly SharedWaterOnShipSystem _waterOnShip = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPumpComponent, PumpUseEvent>(OnPumpUse);
    }

    private void OnPumpUse(EntityUid uid, WaterPumpComponent component, PumpUseEvent args)
    {
        if (args.Cancelled || args.Target is null || args.Handled)
        {
            component.User = null;
            component.DoAfter = null;
            return;
        }

        _waterOnShip.RemoveWater(args.Target.Value, component.WaterCount);
        var audioParams = new Robust.Shared.Audio.AudioParams
        {
            Variation = 0.15f,
            Volume = -10f
        };
        _audio.PlayPvs(MedievalShipSounds.PumpUse, uid, audioParams);
        _appearance.SetData(uid, WaterPumpVisuals.State, WaterPumpState.Active);
        component.UsedTime = _timing.CurTime;
        args.Repeat = true;
        args.Handled = true;
    }
}
