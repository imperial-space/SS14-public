using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.AnomalousCrystal;

public enum AnomalousCrystalKind : byte
{
    /// <summary>Воскрешает мёртвых рядом клоунами.</summary>
    Honk,
    /// <summary>Перекрашивает окрестности в случайную тему.</summary>
    ThemeWarp,
    /// <summary>Выпускает смертельный болт колосса.</summary>
    Emitter,
    /// <summary>Воскрешает мёртвых рядом, но их больше не спасти иначе.</summary>
    DarkReprise,
    /// <summary>Позволяет призракам становиться лайтгейстами.</summary>
    Helpers,
    /// <summary>Позволяет вселиться в мелкое животное рядом.</summary>
    Possessor,
}

/// <summary>
/// Аномальный кристалл (machinery/anomalous_crystal из SS13) — лут колосса.
/// Активируется касанием, у каждого вида свой эффект.
/// </summary>
[RegisterComponent]
public sealed partial class AnomalousCrystalComponent : Component
{
    [DataField(required: true)]
    public AnomalousCrystalKind Kind;

    /// <summary>cooldown_add.</summary>
    [DataField] public float Cooldown = 3f;

    /// <summary>use_time: зарядка перед эффектом.</summary>
    [DataField] public float UseTime;

    [DataField] public SoundSpecifier ActivationSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_effects_break_stone.ogg");
    [DataField] public SoundSpecifier ChargeSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_disable_tech.ogg");

    /// <summary>Описание, видное только призракам (observer_desc).</summary>
    [DataField] public LocId ObserverDesc = "anomalous-crystal-observer-default";

    [DataField] public EntProtoId Confetti = "ImperialAnomalousCrystalConfetti";
    [DataField] public EntProtoId Sparks = "ImperialAnomalousCrystalSparks";
    [DataField] public EntProtoId Projectile = "ImperialColossusBolt";
    [DataField] public EntProtoId Lightgeist = "ImperialLightgeist";
    [DataField] public EntProtoId Cockroach = "MobCockroach";
    [DataField] public EntProtoId ExitAction = "ImperialActionExitPossession";
    [DataField] public string ClownGear = "ClownGear";
    [DataField] public int ThemeRadius = 7;

    [ViewVariables] public bool Active;
    [ViewVariables] public TimeSpan NextUse;
    [ViewVariables] public int ThemeIndex = -1;
    [ViewVariables] public HashSet<(EntityUid Grid, Vector2i Tile)> ConvertedCenters = new();
    [ViewVariables] public HashSet<EntityUid> Clowned = new();
    [ViewVariables] public bool ReadyToDeploy;
}

/// <summary>Животное, в которое вселился игрок через кристалл (obj/structure/closet/stasis).</summary>
[RegisterComponent]
public sealed partial class AnomalousPossessedComponent : Component
{
    [ViewVariables] public EntityUid Body;
    [ViewVariables] public EntityUid? Action;
}

/// <summary>Тело вселившегося: неуязвимо, пока лежит в животном.</summary>
[RegisterComponent]
public sealed partial class AnomalousPossessorBodyComponent : Component;
