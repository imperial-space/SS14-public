using Content.Shared.EntityEffects;

namespace Content.Shared.Imperial.Disease;

/// <summary>Что делает вирусная еда с культурой в образце.</summary>
public enum DiseaseMutateMode : byte
{
    /// <summary>Добавить симптом нужного уровня.</summary>
    Evolve,

    /// <summary>Убрать случайный симптом.</summary>
    Devolve,

    /// <summary>Оставить симптом только в характеристиках.</summary>
    Neuter,
}

/// <summary>
/// chemical_reaction/mix_virus: вирусная еда меняет симптомы культуры в ёмкости.
/// </summary>
public sealed partial class DiseaseMutate : EntityEffectBase<DiseaseMutate>
{
    [DataField]
    public DiseaseMutateMode Mode = DiseaseMutateMode.Evolve;

    /// <summary>Диапазон уровней симптомов, которые может дать эта еда.</summary>
    [DataField]
    public int LevelMin = 1;

    [DataField]
    public int LevelMax = 2;
}
