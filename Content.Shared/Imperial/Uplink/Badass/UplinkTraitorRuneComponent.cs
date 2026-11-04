using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Граффити Синдиката (effect/decal/cleanable/traitor_rune).</summary>
[RegisterComponent]
public sealed partial class UplinkTraitorRuneComponent : Component
{
    [ViewVariables]
    public UplinkRuneStage Stage = UplinkRuneStage.Outline;

    /// <summary>Пять минут после завершения граффити нельзя отмыть.</summary>
    [ViewVariables]
    public TimeSpan? RemovableAt;
}
