using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.AnomalousCrystal;

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
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(3f);

    /// <summary>use_time: зарядка перед эффектом.</summary>
    [DataField] public TimeSpan UseTime;

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

    [DataField]
    public float ActivateEmitterSpeed = 10f;
}
