using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>Метка того, что моба держит в захвате боец — чтобы рвать захват через сопротивление.</summary>
[RegisterComponent]
public sealed partial class MartialGrabbedComponent : Component
{
    [ViewVariables]
    public EntityUid Grabber;
}
