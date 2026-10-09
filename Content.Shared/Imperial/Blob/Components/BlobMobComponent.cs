using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob.Components;

[Serializable, NetSerializable]
public enum BlobMobType : byte
{
    Spore,
    WeakSpore,
    IndependentSpore,
    Zombie,
    Blobbernaut,
}

/// <summary>
/// Миньон блоба (mob/living/basic/blob_minion + datum/component/blob_minion).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobMobComponent : Component
{
    [DataField("mobType")]
    public BlobMobType Type = BlobMobType.Spore;

    /// <summary>death_cloud_size: -1 — нет облака, 0 — маленькое, 1 — обычное.</summary>
    [DataField]
    public int DeathCloudSize = -1;

    /// <summary>Создан ли миньон блобом (порождённые фабрикой; привязка к фабрике).</summary>
    [DataField]
    public bool FactoryBound = true;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Overmind;

    /// <summary>Здоровье ядра оверманда для HUD блоббернаута.</summary>
    [ViewVariables, AutoNetworkedField]
    public int OvermindCoreHealth = -1;

    /// <summary>our_strain: штамм, даже если оверманд мёртв.</summary>
    [ViewVariables]
    public ProtoId<BlobStrainPrototype>? Strain;

    [ViewVariables] public EntityUid? Factory;
    [ViewVariables] public bool Orphaned;
    [ViewVariables] public TimeSpan NextLife;
    [ViewVariables] public TimeSpan NextCorpseCheck;
    [ViewVariables] public EntityUid? CorpseTarget;
    [ViewVariables] public TimeSpan CorpseTargetSince;
    [ViewVariables] public EntityUid? IgnoredCorpse;
}
