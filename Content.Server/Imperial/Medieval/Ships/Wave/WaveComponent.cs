using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Ships.Wave;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class WaveComponent : Component
{

    [DataField]
    public DamageSpecifier DamageTypes = new();

    [DataField]
    public HashSet<EntityUid> HitList = new();

    [DataField]
    public float Strength = 1;
    [DataField]
    public float Direction = 1;

    [DataField]
    public bool DeleteOnCollide = true;

    [DataField]
    public float RepulseRangePerStormLevel = 0.75f;

    [DataField]
    public float RepulseDistancePerStormLevel = 0.3f;
}
