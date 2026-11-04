using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed partial class FissionDoAfterEvent : DoAfterEvent
{
    [DataField]
    public FissionDoAfterAction Action;

    [DataField]
    public NetCoordinates? Location;

    private FissionDoAfterEvent()
    {
    }

    public FissionDoAfterEvent(FissionDoAfterAction action, NetCoordinates? location = null)
    {
        Action = action;
        Location = location;
    }

    public override DoAfterEvent Clone() => this;
}
