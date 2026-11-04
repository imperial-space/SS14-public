using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>/obj/vehicle/sealed/car/clowncar: водитель — клоун, остальных машина похищает.</summary>
[RegisterComponent]
public sealed partial class ImperialClownCarComponent : Component
{
    [DataField]
    public string Container = "clowncar_victims";

    [DataField]
    public string KeyContainer = "clowncar_key";

    /// <summary>max_occupants, водитель тоже считается.</summary>
    [DataField]
    public int MaxOccupants = 50;

    /// <summary>enter_delay.</summary>
    [DataField]
    public TimeSpan EnterDelay = TimeSpan.FromSeconds(2);

    /// <summary>escape_time машины.</summary>
    [DataField]
    public TimeSpan EscapeDelay = TimeSpan.FromSeconds(6);

    /// <summary>attack_hand: открыть багажник.</summary>
    [DataField]
    public TimeSpan TrunkDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan DiceCooldown = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public bool Emagged;
    [ViewVariables]
    public bool Headlights;
    [ViewVariables]
    public ImperialClownCannonMode Cannon = ImperialClownCannonMode.Inactive;
    [ViewVariables]
    public bool CannonActivating;
    [ViewVariables]
    public TimeSpan? CannonSwitchAt;
    [ViewVariables]
    public TimeSpan? CannonVisualAt;
    [ViewVariables]
    public TimeSpan? RecoilEndAt;
    [ViewVariables]
    public TimeSpan NextDice;
    [ViewVariables]
    public TimeSpan? SingularityEndAt;
    [ViewVariables]
    public TimeSpan? OilEndAt;
    [ViewVariables]
    public Robust.Shared.Map.EntityCoordinates? LastOilTile;
    [ViewVariables]
    public TimeSpan? CarBombAt;
    [ViewVariables]
    public TimeSpan NextEngineSound;
    [ViewVariables]
    public TimeSpan NextPartyColor;
    [ViewVariables]
    public TimeSpan NextHorn;
    [ViewVariables]
    public int ThanksCount;

    /// <summary>Водитель, которому выданы действия машины.</summary>
    [ViewVariables]
    public EntityUid? Driver;

    /// <summary>Клоун прошёл enter_delay и может пристегнуться.</summary>
    [ViewVariables]
    public EntityUid? AllowedDriver;

    [ViewVariables]
    public EntityUid? HornAction;
    [ViewVariables]
    public EntityUid? HeadlightsAction;
    [ViewVariables]
    public EntityUid? DumpAction;
    [ViewVariables]
    public EntityUid? RemoveKeyAction;
    [ViewVariables]
    public EntityUid? ExitAction;
    [ViewVariables]
    public EntityUid? DiceAction;
    [ViewVariables]
    public EntityUid? CannonAction;
    [ViewVariables]
    public EntityUid? FireAction;
}
