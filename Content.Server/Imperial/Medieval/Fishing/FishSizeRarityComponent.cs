using Robust.Shared.GameStates;

[RegisterComponent]
public sealed partial class FishSizeRarityComponent : Component
{
    [DataField]
    public string Name = String.Empty;

    [DataField]
    public float Chance = 0;

    [DataField]
    public float PriceMod = 1;

    [DataField]
    public float Scale = 1;

    [DataField]
    public EntityUid? Fisher = EntityUid.Invalid;
}
