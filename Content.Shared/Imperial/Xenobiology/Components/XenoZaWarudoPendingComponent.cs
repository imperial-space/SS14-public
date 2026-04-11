using Robust.Shared.Map;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент, добавляемый существу при поглощении зелья сепии (ZA WARUDO).
/// Хранит обратный отсчёт до применения эффекта и параметры стана.
/// </summary>
[RegisterComponent]
public sealed partial class XenoZaWarudoPendingComponent : Component
{
    /// <summary>Оставшееся время до применения стана (секунды).</summary>
    [DataField]
    public float TimeRemaining;

    /// <summary>Куда был сохранён центр эффекта (координаты применившего).</summary>
    [DataField]
    public MapCoordinates EffectCenter = MapCoordinates.Nullspace;

    /// <summary>Радиус области эффекта (тайлов).</summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>Длительность паралича (секунды).</summary>
    [DataField]
    public float StunDuration = 15f;
}
