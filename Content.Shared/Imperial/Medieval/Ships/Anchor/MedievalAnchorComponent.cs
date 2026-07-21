using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

[Serializable, NetSerializable]
public enum MedievalAnchorVisuals : byte
{
    Enabled
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalAnchorComponent : Component
{
    [DataField("Enabled"), AutoNetworkedField]
    public bool Enabled;

    [DataField("baseUseTime")]
    public float BaseUseTime = 11f;

    [DataField("strengthUseTimeModifier")]
    public float StrengthUseTimeModifier = 0.3f;

    [DataField]
    public TimeSpan? AnchorUsedTime;


    [DataField("islandSearchRange")]
    public float IslandSearchRange = 25f;

    [DataField("wavesTimer")]
    public float WavesTimer = 120f;

    [DataField, AutoNetworkedField]
    public EntityUid? User;
}
