using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/datum/action/changeling: способность генокрада.</summary>
[Prototype]
public sealed partial class ImperialChangelingPowerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>helptext: мелкий шрифт в магазине.</summary>
    [DataField]
    public LocId? HelpText;

    /// <summary>button_icon_state в Imperial/Changeling/actions.rsi.</summary>
    [DataField]
    public string Icon = "changelingsting";

    [DataField]
    public ImperialChangelingCategory Category = ImperialChangelingCategory.Utility;

    /// <summary>dna_cost: очки генов; -1 — CHANGELING_POWER_UNOBTAINABLE.</summary>
    [DataField]
    public int DnaCost = -1;

    /// <summary>CHANGELING_POWER_INNATE: выдаётся сразу.</summary>
    [DataField]
    public bool Innate;

    /// <summary>chemical_cost.</summary>
    [DataField]
    public int ChemicalCost;

    [DataField]
    public int ReqDna;

    [DataField]
    public int ReqAbsorbs;

    /// <summary>req_human: недоступна в низшей форме.</summary>
    [DataField]
    public bool ReqHuman;

    [DataField]
    public ImperialChangelingStat ReqStat = ImperialChangelingStat.Conscious;

    /// <summary>ignores_fakedeath.</summary>
    [DataField]
    public bool IgnoresFakedeath;

    /// <summary>disabled_by_fire.</summary>
    [DataField]
    public bool DisabledByFire = true;

    /// <summary>Действие, которое получает генокрад.</summary>
    [DataField]
    public EntProtoId? Action;
}
