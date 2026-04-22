namespace Content.Shared.Imperial.Fishing.FishingRodComponents;

[RegisterComponent]
public sealed partial class AllowedFishingComponent : Component
{
    [DataField]
    public bool FishingWater = false;
    [DataField]
    public bool FishingLava = false;
    [DataField]
    public bool FishingVoid = false;
}

