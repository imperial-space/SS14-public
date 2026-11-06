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
