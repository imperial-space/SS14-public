namespace Content.Server.Imperial.Lavaland.BlobSporeTrap;

/// <summary>
/// Спора Блоба-ловушка: неподвижная, бессмертная.
/// При приближении моба в радиусе 3×3 клетки — гибает его и исчезает сама.
/// </summary>
[RegisterComponent]
public sealed partial class BlobSporeTrapComponent : Component
{
    /// <summary>
    /// Радиус проверки. 1.5 тайла ≈ квадрат 3×3 клетки.
    /// </summary>
    [DataField]
    public float CheckRadius = 1.5f;

    /// <summary>Интервал проверки (секунды).</summary>
    [DataField]
    public float CheckRate = 0.15f;

    /// <summary>Накопленное время с последней проверки.</summary>
    public float AccumulatedTime;
}
