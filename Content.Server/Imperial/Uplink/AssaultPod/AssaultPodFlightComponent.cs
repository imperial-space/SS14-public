using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Uplink.AssaultPod;

namespace Content.Server.Imperial.Uplink.AssaultPod;

/// <summary>Состояние звуков и меток полёта пода (капсулы или консоли большого пода).</summary>
[RegisterComponent]
public sealed partial class AssaultPodFlightComponent : Component
{
    [ViewVariables]
    public bool StartupPlayed;

    [ViewVariables]
    public bool ArrivalStarted;

    /// <summary>Зацикленный гул гиперпространства.</summary>
    [ViewVariables]
    public EntityUid? TravelStream;

    [ViewVariables]
    public readonly List<EntityUid> Markers = new();
}
