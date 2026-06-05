using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.OfferingSpike;

/// <summary>
/// Шип Подношения — алтарь культа пеплоходцев.
/// Поглощает мёртвые тела в радиусе и из каждых <see cref="CorpsesPerEgg"/> трупов
/// создаёт яйцо, из которого вылупляется новый пеплоходец.
/// </summary>
[RegisterComponent]
public sealed partial class OfferingSpikeComponent : Component
{
    /// <summary>Радиус обнаружения мёртвых тел (в тайлах).</summary>
    [DataField]
    public float CorpseRadius = 1.5f;

    /// <summary>Количество трупов, необходимых для появления одного яйца.</summary>
    [DataField]
    public int CorpsesPerEgg = 2;

    /// <summary>Текущий счётчик поглощённых трупов.</summary>
    [DataField]
    public int AccumulatedCorpses = 0;

    /// <summary>Интервал между циклами поглощения (секунды).</summary>
    [DataField]
    public float CheckInterval = 2.0f;

    /// <summary>Время следующей проверки зоны.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextCheckTime;

    /// <summary>Прототипы яиц для спавна (выбирается случайно).</summary>
    [DataField]
    public List<EntProtoId> EggPrototypes = new() { "FoodEggAshwalker" };

    /// <summary>Звук при поглощении трупа.</summary>
    [DataField]
    public SoundSpecifier AbsorbSound = new SoundPathSpecifier("/Audio/Magic/disintegrate.ogg");

    /// <summary>Звук при появлении яйца.</summary>
    [DataField]
    public SoundSpecifier EggSpawnSound = new SoundPathSpecifier("/Audio/Effects/biomass.ogg");
}
