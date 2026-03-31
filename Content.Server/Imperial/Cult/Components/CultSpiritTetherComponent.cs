namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Привязка культиста к руне Царства духов.
/// Пока компонент активен — гомункулы существуют и дренируют HP каждую секунду.
/// Удаляется если культист отошёл от руны или руна уничтожена.
/// </summary>
[RegisterComponent]
public sealed partial class CultSpiritTetherComponent : Component
{
    /// <summary>UID руны, к которой привязан культист.</summary>
    public EntityUid RuneUid;

    /// <summary>Список активных гомункулов.</summary>
    public List<EntityUid> Homunculi = new();

    /// <summary>Накопитель времени для дрейфа урона.</summary>
    public float DrainAccumulator;

    /// <summary>Интервал (сек) между тиками урона.</summary>
    public const float DrainInterval = 1.0f;

    /// <summary>Брутал-урон за каждый тик на каждого живого гомункула.</summary>
    public const float BrutePerHomunculus = 1.0f;

    /// <summary>Максимальная дистанция до руны (в тайлах) до разрыва привязки.</summary>
    public const float TetherRange = 2.0f;

    /// <summary>Максимальное число гомункулов.</summary>
    public const int MaxHomunculi = 4;
}
