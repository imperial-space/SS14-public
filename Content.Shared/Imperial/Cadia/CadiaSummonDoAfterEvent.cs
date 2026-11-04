using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed partial class CadiaSummonDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Index;

    public CadiaSummonDoAfterEvent(int index)
    {
        Index = index;
    }

    public override DoAfterEvent Clone() => this;
}
