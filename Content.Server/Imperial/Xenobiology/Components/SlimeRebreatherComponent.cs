using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Маска-ребризер: лечит удушье, пока надета.</summary>
[RegisterComponent]
public sealed partial class SlimeRebreatherComponent : Component
{
    [ViewVariables]
    public EntityUid? Wearer;
}
