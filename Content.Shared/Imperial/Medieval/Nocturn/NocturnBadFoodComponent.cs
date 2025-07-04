using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared.NocturnBitten;

[RegisterComponent]
public sealed partial class NocturnBadFoodComponent : Component
{
    [DataField]
    public int TimesCanBeBiten = 5;
    [DataField]
    public int MaxTimesCanBeBiten = 5;
    [DataField]
    public float BloodMultiplier = 0.5f;
    [DataField]
    public bool Fresh = false;

    public TimeSpan StartTime = TimeSpan.FromSeconds(0f);
    public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

}
