namespace Content.Shared.Imperial.Fishing.RandomWeightComponentComponents;

[RegisterComponent]
public sealed partial class RandomWeightComponent : Component
{
    [DataField]
    public float Weight = 0.3f;
    [DataField]
    public float MinWeight = 0.3f;
    [DataField]
    public float MaxWeight = 12f;
    [DataField]
    public int BasePrice = 100;
    [DataField]
    public int PriceCoefficent = 10;
}

