using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Cult.Components;

[RegisterComponent]
public sealed partial class CultConstructComponent : Component
{
    [DataField(required: true)]
    public CultConstructKind Kind;

    [ViewVariables]
    public EntityUid? BuiHolder;
}

public enum CultConstructKind : byte
{
    Artificer,
    Wraith,
    Juggernaut,
    Harvester,
}

[RegisterComponent]
public sealed partial class CultConstructShellComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ConstructProto;

    [DataField]
    public EntProtoId FormationEffectProto = "CultConstructFormationEffectArtificer";
}