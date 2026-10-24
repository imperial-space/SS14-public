using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>/obj/structure/closet/supplypod: капсула снабжения.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialSupplyPodComponent : Component
{
    /// <summary>bluespace: после выгрузки исчезает в искрах.</summary>
    [DataField, AutoNetworkedField]
    public bool Bluespace;

    /// <summary>delays[POD_FALLING], секунды.</summary>
    [DataField]
    public float Falling = 0.4f;

    /// <summary>delays[POD_OPENING].</summary>
    [DataField]
    public float Opening = 3f;

    /// <summary>delays[POD_LEAVING].</summary>
    [DataField]
    public float Leaving = 3f;

    /// <summary>explosionSize: лёгкий радиус и вспышка (0,0,2,3 у обычной, 0,0,1,2 у блюспейс).</summary>
    [DataField]
    public int LightRange = 2;

    [DataField]
    public int FlashRange = 3;

    /// <summary>Угол прилёта (70–110°), задаётся при запуске.</summary>
    [DataField, AutoNetworkedField]
    public float Angle = 90;

    /// <summary>Когда капсула коснётся земли (для анимации падения на клиенте).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LandTime;

    [ViewVariables]
    public TimeSpan? OpenAt;

    [ViewVariables]
    public TimeSpan? LeaveAt;

    [ViewVariables]
    public bool Landed;
}

[Serializable, NetSerializable]
public enum ImperialSupplyPodVisuals : byte
{
    /// <summary>ImperialSupplyPodState.</summary>
    State,

    /// <summary>bool: обломки под капсулой.</summary>
    Rubble,
}

[Serializable, NetSerializable]
public enum ImperialSupplyPodState : byte
{
    Falling,
    Closed,
    Open,
}

/// <summary>/obj/effect/pod_landingzone: метка посадки, вращается до прилёта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialPodLandingZoneComponent : Component
{
    /// <summary>Когда метка появилась и когда капсула сядет (transit + falling).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan Arrival;

    /// <summary>Прототип капсулы.</summary>
    [DataField]
    public EntProtoId Pod = "ImperialSupplyPod";

    /// <summary>Что положить в капсулу (ящик заказа).</summary>
    [DataField]
    public EntProtoId? Cargo;

    /// <summary>delays[POD_TRANSIT].</summary>
    [DataField]
    public float Transit = 3f;

    [ViewVariables]
    public bool SoundPlayed;

    [ViewVariables]
    public EntityUid? PodEntity;
}

/// <summary>/obj/item/supplypod_beacon.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialSupplyPodBeaconComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Console;

    [ViewVariables]
    public bool Ready;

    [ViewVariables]
    public TimeSpan? LaunchedUntil;
}

[Serializable, NetSerializable]
public enum ImperialSupplyPodBeaconVisuals : byte
{
    /// <summary>"", sp_orange, sp_yellow, sp_green.</summary>
    Light,
}

/// <summary>/obj/machinery/computer/cargo/express.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialExpressConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Beacon;

    [DataField]
    public bool Locked = true;

    [ViewVariables]
    public bool UsingBeacon;

    /// <summary>pod_type: блюспейс-капсулы после диска улучшения.</summary>
    [DataField]
    public bool BluespacePods;

    [ViewVariables]
    public TimeSpan NextBeaconPrint;

    [ViewVariables]
    public TimeSpan NextLaunch;
}

/// <summary>/obj/item/disk/cargo/bluespace_pod.</summary>
[RegisterComponent]
public sealed partial class ImperialBluespacePodDiskComponent : Component;

[Serializable, NetSerializable]
public enum ImperialExpressConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialExpressPack
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Category = string.Empty;
    public int Cost;
}

[Serializable, NetSerializable]
public sealed class ImperialExpressConsoleState(
    int points,
    bool locked,
    bool usingBeacon,
    bool hasBeacon,
    bool canBeacon,
    bool canBuyBeacon,
    string beaconName,
    string printMessage,
    string message,
    List<ImperialExpressPack> packs) : BoundUserInterfaceState
{
    public readonly int Points = points;
    public readonly bool Locked = locked;
    public readonly bool UsingBeacon = usingBeacon;
    public readonly bool HasBeacon = hasBeacon;
    public readonly bool CanBeacon = canBeacon;
    public readonly bool CanBuyBeacon = canBuyBeacon;
    public readonly string BeaconName = beaconName;
    public readonly string PrintMessage = printMessage;
    public readonly string Message = message;
    public readonly List<ImperialExpressPack> Packs = packs;
}

[Serializable, NetSerializable]
public enum ImperialExpressAction : byte
{
    LandingCargo,
    LandingBeacon,
    PrintBeacon,
}

[Serializable, NetSerializable]
public sealed class ImperialExpressActionMessage(ImperialExpressAction action) : BoundUserInterfaceMessage
{
    public readonly ImperialExpressAction Action = action;
}

[Serializable, NetSerializable]
public sealed class ImperialExpressBuyMessage(string pack) : BoundUserInterfaceMessage
{
    public readonly string Pack = pack;
}

/// <summary>tool_interact(): резка открытой капсулы сварочным аппаратом.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialSupplyPodCutDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
