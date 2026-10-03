using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Prototypes;

/// <summary>
/// Генетическая мутация (datum/mutation из SS13). Пока мутация активна, носитель получает её компоненты и действия.
/// </summary>
[Prototype]
public sealed partial class GeneticMutationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField]
    public MutationQuality Quality = MutationQuality.Positive;

    /// <summary>
    /// Нестабильность. Положительная тратит стабильность, только если мутацию добавил мутатор;
    /// отрицательная (у негативных мутаций) всегда добавляет запас стабильности.
    /// </summary>
    [DataField]
    public int Instability;

    /// <summary>Не встречается в генах сама: получается только комбинированием.</summary>
    [DataField]
    public bool Locked;

    /// <summary>Сколько позиций последовательности скрыто у спящего гена (difficulty в SS13).</summary>
    [DataField]
    public int Difficulty = 8;

    /// <summary>Вес при выборе мутаций в геном.</summary>
    [DataField]
    public float Weight = 1f;

    [DataField]
    public LocId? GainText;

    [DataField]
    public LocId? LoseText;

    /// <summary>Компоненты, которые даёт мутация. Компоненты, уже бывшие у носителя, не трогаются.</summary>
    [DataField]
    public ComponentRegistry Components = new();

    [DataField]
    public List<EntProtoId> Actions = new();

    /// <summary>Мутации, с которыми эта не уживается.</summary>
    [DataField]
    public List<ProtoId<GeneticMutationPrototype>> Conflicts = new();

    /// <summary>Какие хромосомы можно вставить в мутацию.</summary>
    [DataField]
    public ChromosomeKind Chromosomes = ChromosomeKind.Stabilizer;

    /// <summary>Выпадает ли ген случайно в геном. У обезьяньего гена — нет: он всегда первый блок.</summary>
    [DataField]
    public bool Natural = true;

    /// <summary>Периодические эффекты, пока мутация активна (on_life из SS13).</summary>
    [DataField]
    public List<MutationTick> Ticks = new();
}

/// <summary>
/// Эффект мутации с шансом раз в секунду (SPT_PROB в SS13). Синхронизатор снижает шанс вдвое, сила усиливает эффекты.
/// </summary>
[DataDefinition]
public sealed partial class MutationTick
{
    /// <summary>Шанс срабатывания в секунду, от 0 до 1.</summary>
    [DataField(required: true)]
    public float Chance;

    [DataField]
    public EntityEffect[] Effects = Array.Empty<EntityEffect>();

    /// <summary>Выронить всё из рук.</summary>
    [DataField]
    public bool DropHeld;

    /// <summary>Носитель выкрикивает одну из фраз.</summary>
    [DataField]
    public List<LocId> Say = new();

    [DataField]
    public LocId? Popup;

    /// <summary>Только в сознании.</summary>
    [DataField]
    public bool RequiresConscious = true;

    /// <summary>Шанс растёт по мере падения стабильности (огненный пот: +(100 − стабильность) / 19.5 %).</summary>
    [DataField]
    public bool ScalesWithInstability;
}

[Serializable, NetSerializable]
public enum MutationQuality : byte
{
    Positive,
    MinorNegative,
    Negative,
}

[Flags, Serializable, NetSerializable]
public enum ChromosomeKind : byte
{
    None = 0,

    /// <summary>Нестабильность −20%.</summary>
    Stabilizer = 1 << 0,

    /// <summary>Отдача и побочные эффекты −50%.</summary>
    Synchronizer = 1 << 1,

    /// <summary>Сила мутации +50%.</summary>
    Power = 1 << 2,

    /// <summary>Откат способностей −50%.</summary>
    Energy = 1 << 3,
}

/// <summary>
/// Рецепт комбинирования двух мутаций в третью (datum/generecipe из SS13).
/// </summary>
[Prototype]
public sealed partial class GeneticRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<GeneticMutationPrototype> First;

    [DataField(required: true)]
    public ProtoId<GeneticMutationPrototype> Second;

    [DataField(required: true)]
    public ProtoId<GeneticMutationPrototype> Result;
}
