using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Epsilon;

/// <summary>
/// Аварийная лампа на время протокола: ровное свечение вместо вращающегося луча мигалки
/// (маска и анимация света — только на клиенте).
/// </summary>
[RegisterComponent, Robust.Shared.GameStates.NetworkedComponent]
public sealed partial class EpsilonSteadyLightComponent : Component
{
    /// <summary>Клиент: маска света до протокола.</summary>
    [ViewVariables]
    public string? OriginalMask;
}
