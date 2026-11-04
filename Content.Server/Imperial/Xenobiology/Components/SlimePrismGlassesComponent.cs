using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Призма света из призматических очков.</summary>
[RegisterComponent]
public sealed partial class SlimePrismGlassesComponent : Component
{
    [ViewVariables]
    public Color Color = Color.White;

    [ViewVariables]
    public EntityUid? ColourAction;

    [ViewVariables]
    public EntityUid? PlaceAction;
}
