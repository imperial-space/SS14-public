using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>Сущность культа, чей спрайт управляется CultVisuals (руны, эффекты, постройки, камни).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultVisualsComponent : Component
{
    /// <summary>Анимировать вспышку при активации (руны).</summary>
    [DataField]
    public bool Rune;

    /// <summary>Базовое состояние для "_off" у незакреплённых построек.</summary>
    [DataField]
    public string? AnchorState;

    /// <summary>RSI, которым притворяется скрытая рунная дверь.</summary>
    [DataField]
    public string? ConcealedRsi;
}
