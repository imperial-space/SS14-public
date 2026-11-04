namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>grenade/clusterbuster/slime: разбрасывает случайные экстракты.</summary>
[RegisterComponent]
public sealed partial class SlimeGrenadeComponent : Component
{
    /// <summary>clusterbuster/slime/volatile: экстракты сами активируются.</summary>
    [DataField]
    public bool Volatile;

    /// <summary>clusterbuster/segment: не делится дальше.</summary>
    [DataField]
    public bool Segment;

    [DataField]
    public int MinSpawned = 4;

    [DataField]
    public int MaxSpawned = 8;

    [DataField]
    public float SegmentChance = 0.35f;

    /// <summary>det_time гранаты при ручном взведении.</summary>
    [DataField]
    public float Delay = 5f;

    [ViewVariables]
    public bool Armed;
}
