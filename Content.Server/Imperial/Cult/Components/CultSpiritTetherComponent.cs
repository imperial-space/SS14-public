using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

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
    [ViewVariables]
    public EntityUid RuneUid;

    /// <summary>Список активных гомункулов.</summary>
    [ViewVariables]
    public List<EntityUid> Homunculi = new();

    /// <summary>Накопитель времени для дрейфа урона.</summary>
    [ViewVariables]
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
