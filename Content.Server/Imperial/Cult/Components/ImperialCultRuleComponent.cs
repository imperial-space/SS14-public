using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Культ крови (datum/team/cult + dynamic roundstart blood_cult).
/// </summary>
[RegisterComponent]
public sealed partial class ImperialCultRuleComponent : Component
{
    /// <summary>Стартовое снаряжение: ритуальный кинжал и 10 листов рунного металла.</summary>
    [DataField]
    public List<EntProtoId> StartingItems = new() { "CultDagger", "CultRunedMetalTen" };

    /// <summary>SUMMON_POSSIBILITIES.</summary>
    [DataField]
    public int SummonSpotCount = 3;

    /// <summary>CULT_HIGHPOP_THRESHOLD / CULT_RISEN_* / CULT_ASCENDENT_* (BandaStation).</summary>
    [DataField] public int HighpopThreshold = 70;
    [DataField] public float RisenLowpop = 0.2f;
    [DataField] public float AscendentLowpop = 0.3f;
    [DataField] public float RisenHighpop = 0.1f;
    [DataField] public float AscendentHighpop = 0.2f;

    /// <summary>SOULS_TO_REVIVE.</summary>
    [DataField] public int SoulsToRevive = 3;

    // ───── Команда ─────

    [ViewVariables] public List<EntityUid> Members = new();
    [ViewVariables] public List<EntityUid> TrueCultists = new();
    [ViewVariables] public int SizeAtMaximum;
    [ViewVariables] public EntityUid? LeaderMind;
    [ViewVariables] public bool LeaderPassedOn;
    [ViewVariables] public TimeSpan? LeaderPending;
    [ViewVariables] public bool ReckoningComplete;
    [ViewVariables] public bool Risen;
    [ViewVariables] public bool Ascendent;

    // ───── Цели ─────

    /// <summary>Ум цели жертвоприношения (datum/objective/sacrifice).</summary>
    [ViewVariables] public EntityUid? SacrificeTarget;
    [ViewVariables] public bool SacrificeDone;

    /// <summary>Места призыва (eldergod summon_spots): маяки навкарты.</summary>
    [ViewVariables] public List<EntityUid> SummonSpots = new();
    [ViewVariables] public List<string> SummonSpotNames = new();
    [ViewVariables] public bool NarSieSummoned;
    [ViewVariables] public bool NarSieKilled;
    [ViewVariables] public bool SummonAnnounced;

    /// <summary>Музыка ритуала уже звучала в этом раунде.</summary>
    [ViewVariables] public bool RitualMusicPlayed;

    /// <summary>GLOB.sacrificed / GLOB.sacrifices_used (начинается с -SOULS_TO_REVIVE).</summary>
    [ViewVariables] public int Sacrificed;
    [ViewVariables] public int SacrificesUsed = -3;

    // ───── Кровавая метка ─────

    [ViewVariables] public EntityUid? BloodTarget;
    [ViewVariables] public EntityUid? BloodTargetMarker;
    [ViewVariables] public TimeSpan? BloodTargetEnd;

    // ───── Нар'Си ─────

    [ViewVariables] public EntityUid? NarSie;
    [ViewVariables] public int EndStage;
    [ViewVariables] public TimeSpan NextEndStage;
    [ViewVariables] public bool Resolved;

    /// <summary>Сколько раз проклинали шаттл (MAX_SHUTTLE_CURSES).</summary>
    [ViewVariables] public int ShuttleCurses;
    [ViewVariables] public TimeSpan FirstCurseTime;
    [ViewVariables] public List<string> RemainingCurses = new();
}
