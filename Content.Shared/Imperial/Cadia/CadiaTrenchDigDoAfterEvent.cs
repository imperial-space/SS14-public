using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed partial class CadiaTrenchDigDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Spot;

    public CadiaTrenchDigDoAfterEvent(NetCoordinates spot)
    {
        Spot = spot;
    }

    public override DoAfterEvent Clone() => this;
}
