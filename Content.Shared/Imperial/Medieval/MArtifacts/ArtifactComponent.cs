
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Artifacts;

[NetworkedComponent, RegisterComponent]
public sealed partial class ArtifactComponent : Component
{
    public Dictionary<string, EntityUid> Abilities = new();
    [DataField("startAbilities")]
    public List<string>? StartAbilities;
    [DataField("amount")]
    public int AmountToRandomize = 1;
    [ViewVariables(VVAccess.ReadWrite)]
    public string CurrentSprite = "";
    [DataField("spriteData")]
    public string SpriteDataset = "";
}
[Serializable, NetSerializable]
public sealed class ArtifactSpriteState : ComponentState
{
    public string Path = default!;
}
