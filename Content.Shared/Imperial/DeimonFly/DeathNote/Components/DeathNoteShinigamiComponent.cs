namespace Content.Shared.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Маркер сущности бога смерти. Компонент не использует обычную призрачную видимость.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteShinigamiComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ushort VisibilityLayer = DeathNoteVisibilityLayers.Shinigami;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool HadNormalVisibility;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AddedVisibilityLayer;
}

/// <summary>
/// Маркер бога смерти, изолированный во втором канале владения Тетрадью смерти.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteShinigami2Component : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ushort VisibilityLayer = DeathNoteVisibilityLayers.Shinigami2;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool HadNormalVisibility;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AddedVisibilityLayer;
}

/// <summary>
/// Маркер бога смерти, изолированный в третьем канале владения Тетрадью смерти.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteShinigami3Component : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ushort VisibilityLayer = DeathNoteVisibilityLayers.Shinigami3;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool HadNormalVisibility;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AddedVisibilityLayer;
}
