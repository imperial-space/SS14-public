using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент заряженного ядра ксено-слайма (XenoChargedSlimeCore).
///
/// Ядро создаётся когда взрослый слайм одного цвета поглощает 10 экстрактов другого цвета
/// (механика кроссбридинга). Обе части определяют итоговый эффект.
///
/// Цикл использования:
///   1. Создаётся с SlimeColor и ExtractColor заданными при спавне.
///   2. Вколоть PlasmaRequired единиц Plasma (через шприц/гипоспрей).
///   3. После зарядки: активировать в руке → применяется эффект.
/// </summary>
[RegisterComponent]
public sealed partial class XenoChargedSlimeCoreComponent : Component
{
    /// <summary>Цвет слайма-основы (база = тип эффекта).</summary>
    [DataField]
    public XenoSlimeColor SlimeColor = XenoSlimeColor.Grey;

    /// <summary>Цвет экстрактов (наполнитель = применение эффекта).</summary>
    [DataField]
    public XenoSlimeColor ExtractColor = XenoSlimeColor.Grey;

    /// <summary>Заряжено ли ядро плазмой.</summary>
    [DataField]
    public bool PlasmaCharged = false;

    /// <summary>Сколько единиц плазмы нужно вколоть для зарядки.</summary>
    [DataField]
    public float PlasmaRequired = 10f;

    /// <summary>Название раствора для инъекции.</summary>
    [DataField]
    public string SolutionName = "chemicals";
}
