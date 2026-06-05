using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.ColossusLoot;

[RegisterComponent]
public sealed partial class VoiceOfGodComponent : Component
{
    [DataField]
    public float Radius = 7f;

    [DataField]
    public float BaseCooldown = 15f;

    [DataField]
    public EntProtoId PrepareAction = "ActionVoiceOfGodPrepare";

    [ViewVariables]
    public EntityUid? PrepareActionEntity;

    [ViewVariables]
    public TimeSpan NextUse;
}
