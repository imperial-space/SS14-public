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

    public bool Emagged;
    public bool Headlights;
    public ImperialClownCannonMode Cannon = ImperialClownCannonMode.Inactive;
    public bool CannonActivating;
    public TimeSpan? CannonSwitchAt;
    public TimeSpan? CannonVisualAt;
    public TimeSpan? RecoilEndAt;
    public TimeSpan NextDice;
    public TimeSpan? SingularityEndAt;
    public TimeSpan? OilEndAt;
    public Robust.Shared.Map.EntityCoordinates? LastOilTile;
    public TimeSpan? CarBombAt;
    public TimeSpan NextEngineSound;
    public TimeSpan NextPartyColor;
    public TimeSpan NextHorn;
    public int ThanksCount;

    /// <summary>Водитель, которому выданы действия машины.</summary>
    public EntityUid? Driver;

    /// <summary>Клоун прошёл enter_delay и может пристегнуться.</summary>
    public EntityUid? AllowedDriver;

    public EntityUid? HornAction;
    public EntityUid? HeadlightsAction;
    public EntityUid? DumpAction;
    public EntityUid? RemoveKeyAction;
    public EntityUid? ExitAction;
    public EntityUid? DiceAction;
    public EntityUid? CannonAction;
    public EntityUid? FireAction;
}

/// <summary>Похищенный пассажир машины (VEHICLE_CONTROL_KIDNAPPED).</summary>
[RegisterComponent]
public sealed partial class ImperialClownCarKidnappedComponent : Component
{
    public EntityUid Car;
    public EntityUid? ThankAction;
    public EntityUid? ExitAction;
}

/// <summary>Сплющенный машиной (/datum/element/squish).</summary>
[RegisterComponent]
public sealed partial class ImperialClownCarSquishedComponent : Component
{
    public TimeSpan EndAt;
    public System.Numerics.Vector2 OldScale = System.Numerics.Vector2.One;
}

public enum ImperialClownCannonMode : byte
{
    Inactive,
    Busy,
    Ready,
}

[Serializable, NetSerializable]
public enum ImperialClownCarVisuals : byte
{
    State,
    Singularity,
}

[Serializable, NetSerializable]
public enum ImperialClownCarState : byte
{
    Normal,
    ToFire,
    Fire,
    FromFire,
    Recoil,
}

public sealed partial class ImperialClownCarHornEvent : InstantActionEvent;
public sealed partial class ImperialClownCarHeadlightsEvent : InstantActionEvent;
public sealed partial class ImperialClownCarDumpEvent : InstantActionEvent;
public sealed partial class ImperialClownCarRemoveKeyEvent : InstantActionEvent;
public sealed partial class ImperialClownCarExitEvent : InstantActionEvent;
public sealed partial class ImperialClownCarDiceEvent : InstantActionEvent;
public sealed partial class ImperialClownCarCannonEvent : InstantActionEvent;
public sealed partial class ImperialClownCarFireEvent : WorldTargetActionEvent;
public sealed partial class ImperialClownCarThankEvent : InstantActionEvent;

/// <summary>Сесть в машину (mob_try_enter).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialClownCarEnterDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Запихнуть другого в машину (mob_try_forced_enter).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialClownCarForceDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Выбраться из багажника (mob_try_exit похищенного).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialClownCarEscapeDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Открыть багажник снаружи (attack_hand).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialClownCarTrunkDoAfterEvent : SimpleDoAfterEvent;

/// <summary>set_eye_blur_if_lower после удара об оленя.</summary>
[RegisterComponent]
public sealed partial class ImperialClownCarBlurComponent : Component
{
    public TimeSpan EndAt;
}
