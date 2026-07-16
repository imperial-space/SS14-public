namespace Content.Server.Imperial.Lavaland.BlobFatGate;

/// <summary>
/// Клетка Блоба: пропускает только сытых (HungerThreshold.Overfed).
/// Система периодически сканирует ближайшие сущности и убирает физический блок
/// пока рядом стоит хотя бы одна «жирная» сущность.
/// </summary>
[RegisterComponent]
public sealed partial class BlobFatGateComponent : Component
{
    /// <summary>Радиус сканирования сытых сущностей.</summary>
    [DataField]
    public float CheckRange = 1.0f;

    /// <summary>Интервал проверки (секунды).</summary>
    [DataField]
    public float CheckRate = 0.1f;

    /// <summary>Накопленное время с последней проверки.</summary>
    public float AccumulatedTime;

    /// <summary>Открыт ли проход прямо сейчас.</summary>
    public bool IsOpen;
}
