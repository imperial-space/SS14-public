using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

/// <summary>Положение камеры стержня (CHAMBER_* из SS220 NGCR).</summary>
[Serializable, NetSerializable]
public enum FissionChamberState : byte
{
    Down = 1,
    Up = 2,
    Open = 3,
    OverloadIdle = 4,
    OverloadActive = 5,
}

[Serializable, NetSerializable]
public enum FissionRodCategory : byte
{
    Fuel,
    Moderator,
    Coolant,
}

/// <summary>Цветной индикатор камеры (red/orange/green/blue/overload).</summary>
[Serializable, NetSerializable]
public enum FissionChamberStatus : byte
{
    None,
    Red,
    Orange,
    Green,
    Blue,
    Overload,
}

[Serializable, NetSerializable]
public enum FissionChamberVisuals : byte
{
    State,
    Status,
    Rod,
    Durability,
    Welded,
    Panel,
}

[Serializable, NetSerializable]
public enum FissionChamberLayers : byte
{
    Base,
    Welded,
    Status,
    Display,
    Durability,
    RodOverlay,
    Door,
}

[Serializable, NetSerializable]
public enum FissionReactorState : byte
{
    Off,
    Starting,
    On,
    Hot,
    Overheat,
    Maintenance,
    Broken,
    Meltdown,
}

[Serializable, NetSerializable]
public enum FissionReactorVisuals : byte
{
    State,
    Rods,
}

[Serializable, NetSerializable]
public enum FissionReactorLayers : byte
{
    Base,
    Rods,
}

[Serializable, NetSerializable]
public enum FissionMachineVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum FissionMachineLayers : byte
{
    Base,
}

/// <summary>Шаги ремонта расплавленного реактора (REACTOR_NEEDS_*).</summary>
[Serializable, NetSerializable]
public enum FissionRepairStep : byte
{
    Digging = 1,
    Crowbar = 2,
    Plastitanium = 3,
    Wrench = 4,
    Welding = 5,
    Plasteel = 6,
    Screwdriver = 7,
}

[Serializable, NetSerializable]
public enum FissionDoAfterAction : byte
{
    ChamberRaise,
    ChamberLower,
    ChamberWeld,
    ChamberRepair,
    ReactorDig,
    ReactorCrowbar,
    ReactorPlastitanium,
    ReactorPatch,
    ReactorWrench,
    ReactorWeld,
    ReactorPlasteel,
    ReactorScrew,
    ReactorCloseVent,
    ReactorControlRod,
    ReactorTerminal,
    NodeFlip,
    WasteClear,
}

[Serializable, NetSerializable]
public sealed partial class FissionDoAfterEvent : DoAfterEvent
{
    [DataField]
    public FissionDoAfterAction Action;

    [DataField]
    public NetCoordinates? Location;

    private FissionDoAfterEvent()
    {
    }

    public FissionDoAfterEvent(FissionDoAfterAction action, NetCoordinates? location = null)
    {
        Action = action;
        Location = location;
    }

    public override DoAfterEvent Clone() => this;
}

// ────────────── Консоль наблюдения ──────────────

[Serializable, NetSerializable]
public enum FissionMonitorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FissionGasEntry
{
    public string Gas;
    public float Amount;
    public float Portion;
    public string? Description;

    public FissionGasEntry(string gas, float amount, float portion, string? description)
    {
        Gas = gas;
        Amount = amount;
        Portion = portion;
        Description = description;
    }
}

[Serializable, NetSerializable]
public sealed class FissionMonitorUiState : BoundUserInterfaceState
{
    public bool HasReactor;
    public bool Controlling;
    public bool Venting;
    public float Integrity;
    public float PowerKilowatts;
    public float Temperature;
    public float Pressure;
    public float Coefficient;
    public float Throttle;
    public float OperatingPower;
    public List<FissionGasEntry> Gases = new();
    public List<FissionGasEntry> ModeratorGases = new();
}

[Serializable, NetSerializable]
public sealed class FissionSetThrottleMessage(float throttle) : BoundUserInterfaceMessage
{
    public readonly float Throttle = throttle;
}

[Serializable, NetSerializable]
public sealed class FissionToggleVentMessage : BoundUserInterfaceMessage;

// ────────────── Центрифуга ──────────────

[Serializable, NetSerializable]
public enum FissionCentrifugeUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FissionCentrifugeUiState(List<string> options) : BoundUserInterfaceState
{
    public readonly List<string> Options = options;
}

[Serializable, NetSerializable]
public sealed class FissionCentrifugeChooseMessage(string result) : BoundUserInterfaceMessage
{
    public readonly string Result = result;
}

/// <summary>Метка для клиентского визуализатора камеры (анимации подъёма, створок и слой отрисовки).</summary>
[RegisterComponent]
public sealed partial class FissionChamberVisualsComponent : Robust.Shared.GameObjects.Component
{
    [ViewVariables]
    public FissionChamberState? LastState;
}

// ────────────── Фабрикатор стержней ──────────────

[Serializable, NetSerializable]
public enum FissionFabricatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FissionRodDesign
{
    public string Id = string.Empty;
    public FissionRodCategory Category;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public float PowerAmount;
    public float PowerAmpMod = 1;
    public float HeatAmount;
    public float HeatAmpMod = 1;
    /// <summary>Отрицательное — бесконечная прочность.</summary>
    public float MaxDurability;
    public string? HeatEnrichment;
    public float HeatEnrichmentRequirement;
    public string? PowerEnrichment;
    public float PowerEnrichmentRequirement;
    public List<string> NeighborRequirements = new();
    public List<FissionMaterialEntry> Materials = new();
}

[Serializable, NetSerializable]
public sealed class FissionMaterialEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public int Amount;
    public int Sheets;
}

[Serializable, NetSerializable]
public sealed class FissionFabricatorUiState : BoundUserInterfaceState
{
    public List<FissionRodDesign> Designs = new();
    public List<FissionMaterialEntry> Resources = new();
}

[Serializable, NetSerializable]
public sealed class FissionFabricateMessage(string design) : BoundUserInterfaceMessage
{
    public readonly string Design = design;
}

[Serializable, NetSerializable]
public sealed class FissionEjectMaterialMessage(string material, int sheets) : BoundUserInterfaceMessage
{
    public readonly string Material = material;
    public readonly int Sheets = sheets;
}
