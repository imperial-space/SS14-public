using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

[Serializable, NetSerializable]
public sealed partial class MorphTakeFormDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Index;

    private MorphTakeFormDoAfterEvent()
    {
    }

    public MorphTakeFormDoAfterEvent(int index)
    {
        Index = index;
    }

    public override DoAfterEvent Clone() => this;
}
