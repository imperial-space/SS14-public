using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

/// <summary>Какое из искусств TG реализует прототип.</summary>
[Serializable, NetSerializable]
public enum MartialArtKind : byte
{
    Cqc,
    CloseQuartersCooking,
    SleepingCarp,
    Boxing,
    EvilBoxing,
    HunterBoxing,
    Wrestling,
    PlasmaFist,
    PlasmaFistNoBomb,
    PsychoticBrawling,
    MushroomPunch,
    JungleArts,
    SpidersBite,
    KazaRuk,
    CorporateJudo,
}
