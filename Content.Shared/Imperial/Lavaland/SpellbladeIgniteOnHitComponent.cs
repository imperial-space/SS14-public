using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland;

[RegisterComponent]
public sealed partial class SpellbladeIgniteOnHitComponent : Component
{
    [DataField(required: true)]
    public EntProtoId FireTilePrototype = default!;
}