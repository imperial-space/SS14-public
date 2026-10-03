using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Lavaland.Resonator;

public enum ResonatorMode : byte
{
    Auto,
    Manual,
    Matrix,
}

[RegisterComponent]
public sealed partial class ResonatorComponent : Component
{
    [DataField]
    public ResonatorMode Mode = ResonatorMode.Auto;

    [DataField]
    public int FieldLimit = 4;

    [DataField]
    public float QuickBurstMod = 0.8f;

    [DataField]
    public float AddingFailure = 50f;

    [DataField]
    public bool CanMatrix = false;

    public List<EntityUid> Fields = new();
}
