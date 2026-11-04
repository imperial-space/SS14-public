using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

[Serializable, NetSerializable]
public sealed partial class ImperialModCaptureDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Target;

    [DataField]
    public bool Packup;

    public ImperialModCaptureDoAfterEvent(NetCoordinates target, bool packup)
    {
        Target = target;
        Packup = packup;
    }

    public override DoAfterEvent Clone() => this;
}
