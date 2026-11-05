using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

[Serializable, NetSerializable]
public sealed partial class ImperialProcessDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Entry;

    private ImperialProcessDoAfterEvent()
    {
    }

    public ImperialProcessDoAfterEvent(int entry)
    {
        Entry = entry;
    }

    public override DoAfterEvent Clone() => this;
}
