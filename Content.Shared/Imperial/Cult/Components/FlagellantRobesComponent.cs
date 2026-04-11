using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Маркер на предмете одежды (мантия флагелланта).
/// Активирует удвоение входящего урона и увеличение скорости на носителе.
/// </summary>
[RegisterComponent]
public sealed partial class FlagellantRobesComponent : Component
{
    /// <summary>Множитель входящего урона (настраивается через YAML).</summary>
    [DataField]
    public float DamageMultiplier = 2.0f;
}
