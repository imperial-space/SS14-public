using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

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

    [DataField]
    public DamageSpecifier HitDeerDamage = new() { DamageDict = { ["Blunt"] = 200 } };

    [DataField]
    public TimeSpan PartyColorDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan EngineSoundDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan DumpMobsDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan HornDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan CarBombDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan CheckMobsDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan EndDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan RunOverDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan SingularityEndDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan OilEndDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan CannonVisualDelay = TimeSpan.FromSeconds(1.8);

    [DataField]
    public TimeSpan CannonSwitchDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan RecoilEndDelay = TimeSpan.FromSeconds(0.3);

    [DataField]
    public float UpdateTotalIntensity = 10f;

    [DataField]
    public float UpdateSlope = 5f;

    [DataField]
    public float UpdateMaxTileIntensity = 3f;

    [DataField]
    public FixedPoint2 UpdateQuantity = 5;

    [DataField]
    public float ExitBaseThrowSpeed = 10f;

    [DataField]
    public float DamagedChance = 0.33f;

    [DataField]
    public int DamagedRange = 4;

    [DataField]
    public float CheckMobsRange = 0.5f;

    [DataField]
    public float HitDeerChance = 0.35f;

    [DataField]
    public float HitDeerChance2 = 0.5f;

    [DataField]
    public FixedPoint2 HitDeerQuantity = 15;

    [DataField]
    public int DiceRange = 200;

    [DataField]
    public int DiceRange2 = 4;

    [DataField]
    public float DiceRange3 = 6f;

    [DataField]
    public float FireBaseThrowSpeed = 10f;
}
