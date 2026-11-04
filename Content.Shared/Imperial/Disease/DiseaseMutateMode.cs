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
