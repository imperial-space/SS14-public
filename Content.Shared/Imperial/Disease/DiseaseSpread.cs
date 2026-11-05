using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

// Вирусология 1 в 1 с SS13 (code/datums/diseases).

/// <summary>DISEASE_SPREAD_*: как болезнь передаётся между носителями.</summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseSpread : byte
{
    None = 0,

    /// <summary>Особый способ передачи, прописанный самой болезнью.</summary>
    Special = 1 << 0,

    /// <summary>Не передаётся.</summary>
    NonContagious = 1 << 1,

    /// <summary>Через кровь: укол, переливание.</summary>
    Blood = 1 << 2,

    /// <summary>Через жидкости: еда, питьё, брызги.</summary>
    ContactFluids = 1 << 3,

    /// <summary>Через кожу: прикосновение.</summary>
    ContactSkin = 1 << 4,

    /// <summary>По воздуху.</summary>
    Airborne = 1 << 5,
}
