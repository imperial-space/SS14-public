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

/// <summary>stack/ore/bluespace_crystal: телепорт на blink_range при использовании и броске.</summary>
[RegisterComponent]
public sealed partial class SlimeBluespaceCrystalComponent : Component
{
    [DataField]
    public int BlinkRange = 8;
}

/// <summary>obj/effect/timestop: всё в радиусе замирает.</summary>
[RegisterComponent]
public sealed partial class SlimeTimestopComponent : Component
{
    [DataField]
    public float Range = 2f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(14);

    [ViewVariables]
    public TimeSpan End;

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Кто не замирает (TRAIT_TIME_STOP_IMMUNE у владельца эффекта).</summary>
    [ViewVariables]
    public HashSet<EntityUid> Immune = new();
}

/// <summary>blueprints/slime: делает зону вокруг пригодной для консоли ксенобиологии.</summary>
[RegisterComponent]
public sealed partial class SlimeBlueprintComponent : Component
{
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId Zone = "XenobioZoneMarker";
}

/// <summary>Зона ксенобиологии (area/station/science/xenobiology): консоль действует внутри.</summary>
[RegisterComponent]
public sealed partial class XenobioZoneComponent : Component
{
    [DataField]
    public float Range = 7f;
}
