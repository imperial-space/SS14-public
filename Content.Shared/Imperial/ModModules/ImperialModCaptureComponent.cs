using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>criminalcapture / patienttransport: мешок из твёрдого света вокруг цели.</summary>
[RegisterComponent]
public sealed partial class ImperialModCaptureComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Bag;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan PackupTime = TimeSpan.FromSeconds(1);

    /// <summary>Расход, пока мешок существует (DEFAULT_CHARGE_DRAIN × 3).</summary>
    [DataField]
    public float LinkedPower = 3f;

    [DataField]
    public float MaxRange = 9f;

    [ViewVariables]
    public EntityUid? LinkedBag;

    [ViewVariables]
    public TimeSpan NextDrain;

    [DataField]
    public TimeSpan DrainDelay = TimeSpan.FromSeconds(1);
}
