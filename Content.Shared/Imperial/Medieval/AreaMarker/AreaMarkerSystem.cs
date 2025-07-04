using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.AreaMarker;

public sealed class AreaMarkerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    private Entity<AudioComponent>? _audioEntity;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AreaMarkerComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<AreaMarkerComponent> ent, ref StartCollideEvent args)
    {
        if (!TryComp<AreaMarkerInvokerComponent>(args.OtherEntity, out var invokerComponent))
        {
            return;
        }

        if (invokerComponent.LastArea == ent.Comp.AreaName)
        {
            return;
        }

        invokerComponent.LastArea = ent.Comp.AreaName;
        Dirty(args.OtherEntity, invokerComponent);

        if (!_netManager.IsClient ||
            !_timing.IsFirstTimePredicted ||
            !_playerManager.LocalEntity.HasValue ||
            _playerManager.LocalEntity.Value != args.OtherEntity)
        {
            return;
        }

        var message = Loc.GetString("wrapped-area-marker-message", ("area", Loc.GetString(ent.Comp.AreaName)), ("fontSize", ent.Comp.FontSize));

        var ev = new AreaMarkerAnnounceEvent(message);
        RaiseLocalEvent(ref ev);

        if (_audioEntity.HasValue)
        {
            _audioSystem.Stop(_audioEntity);
        }

        _audioEntity = _audioSystem.PlayGlobal(ent.Comp.AudioPath, args.OtherEntity);
    }
}
