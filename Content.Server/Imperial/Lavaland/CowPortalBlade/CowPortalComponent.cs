using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.CowPortalBlade;

/// <summary>
/// Портал, из которого спавнятся коровы.
/// Удаляется после одного использования (TimedDespawn).
/// </summary>
[RegisterComponent]
public sealed partial class CowPortalComponent : Component
{
    /// <summary>Прототип коровы.</summary>
    [DataField]
    public EntProtoId CowPrototype = "MobCow";

    /// <summary>Сколько коров выйдет из портала.</summary>
    [DataField]
    public int CowCount = 20;

    /// <summary>Радиус рассеивания коров вокруг портала.</summary>
    [DataField]
    public float SpawnRadius = 2.5f;

    /// <summary>Задержка перед появлением коров (мс).</summary>
    [DataField]
    public int SpawnDelayMs = 1500;
}
