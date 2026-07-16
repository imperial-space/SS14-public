using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.NecropolisSpike;

/// <summary>
/// Шип Некрополя — неподвижная структура, периодически порождающая существ Лаваленда.
/// При уничтожении оставляет сундук и создаёт бездну вокруг себя.
/// </summary>
[RegisterComponent]
public sealed partial class NecropolisSpikeComponent : Component
{
    /// <summary>Возможные призываемые существа.</summary>
    [DataField]
    public List<EntProtoId> SpawnPrototypes = new()
    {
        "MobLegionNormal",
        "MobGoliath",
        "MobWatcherMagmawing",
    };

    /// <summary>Максимальное количество порождений одновременно.</summary>
    [DataField]
    public int MaxSpawns = 3;

    /// <summary>Интервал между попытками спавна (секунды).</summary>
    [DataField]
    public float SpawnInterval = 20f;

    /// <summary>Момент следующей попытки спавна.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSpawnTime;

    /// <summary>Радиус разброса при спавне.</summary>
    [DataField]
    public float SpawnRadius = 2f;

    /// <summary>Радиус бездны вокруг шипа (в тайлах).</summary>
    [DataField]
    public float AbyssRadius = 2f;

    /// <summary>Задержка создания бездны после уничтожения шипа (секунды).</summary>
    [DataField]
    public float AbyssDelay = 5f;

    /// <summary>Момент создания бездны; нулевое значение = ещё не запланировано.</summary>
    public TimeSpan AbyssTime = TimeSpan.Zero;

    /// <summary>Отслеживаемые порождения шипа.</summary>
    public List<EntityUid> SpawnedMobs = new();

    /// <summary>Шип уничтожен — ждём создания бездны.</summary>
    public bool IsDead;
}
