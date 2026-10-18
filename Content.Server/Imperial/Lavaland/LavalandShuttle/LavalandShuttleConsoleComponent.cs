using Content.Shared.Imperial.Lavaland.LavalandShuttle;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.LavalandShuttle;

/// <summary>
/// Консоль шаттла утилизаторов (computer/shuttle/mining из SS13): станция и аванпост лаваленда.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandShuttleConsoleComponent : Component
{
    /// <summary>
    /// Грид аванпоста переработки на лаваленде. Выставляет LavalandPlanetRuleSystem.
    /// </summary>
    [DataField]
    public EntityUid? RecyclingOutpostGrid;

    /// <summary>ignitionTime мобильного порта SS13.</summary>
    [DataField]
    public float IgnitionTime = 5.5f;

    /// <summary>callTime мобильного порта SS13.</summary>
    [DataField]
    public float TravelTime = 10f;

    /// <summary>Срочная отправка: прогрев.</summary>
    [DataField]
    public float ExpressIgnitionTime = 1f;

    /// <summary>Срочная отправка: перелёт.</summary>
    [DataField]
    public float ExpressTravelTime = 3f;

    /// <summary>Откат срочной отправки.</summary>
    [DataField]
    public TimeSpan ExpressCooldown = TimeSpan.FromMinutes(10);

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [ViewVariables]
    public TimeSpan NextExpress;

    [ViewVariables]
    public LavalandShuttleDestination SelectedDestination = LavalandShuttleDestination.None;

    /// <summary>Куда летит шаттл (для «In transit to …»).</summary>
    [ViewVariables]
    public LavalandShuttleDestination TravelDestination = LavalandShuttleDestination.None;

    [ViewVariables]
    public TimeSpan NextUiUpdate;
}
