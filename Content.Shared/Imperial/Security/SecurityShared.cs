using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Телепорт гулага (machinery/gulag_teleporter) ───────────────────────────

/// <summary>
/// Телепорт трудового лагеря. Общий — чтобы клиент разрешал перетаскивание заключённого.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GulagTeleporterComponent : Component
{
    public const string ContainerId = "gulag-teleporter-occupant";

    [ViewVariables]
    public ContainerSlot Occupant = default!;

    /// <summary>state_open: дверь открыта, внутрь можно посадить.</summary>
    [DataField, AutoNetworkedField]
    public bool Open = true;

    /// <summary>locked.</summary>
    [DataField, AutoNetworkedField]
    public bool Locked;

    /// <summary>breakout_time: 60 секунд, вдвое меньше для связанного при открытом замке.</summary>
    [DataField]
    public TimeSpan BreakoutTime = TimeSpan.FromSeconds(60);

    /// <summary>message_cooldown «дверь не поддаётся».</summary>
    [ViewVariables]
    public TimeSpan NextMessage;

    [ViewVariables]
    public bool BreakingOut;

    /// <summary>jumpsuit_type.</summary>
    [DataField]
    public string JumpsuitProto = "ClothingUniformJumpsuitPrisoner";

    /// <summary>shoes_type.</summary>
    [DataField]
    public string ShoesProto = "ClothingShoesColorOrange";
}

public sealed class SharedGulagTeleporterSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GulagTeleporterComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GulagTeleporterComponent, CanDropTargetEvent>(OnCanDrop);
    }

    private void OnInit(Entity<GulagTeleporterComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Occupant = _container.EnsureContainer<ContainerSlot>(ent, GulagTeleporterComponent.ContainerId);
    }

    private void OnCanDrop(Entity<GulagTeleporterComponent> ent, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= ent.Comp.Open && ent.Comp.Occupant.ContainedEntity == null && HasComp<MobStateComponent>(args.Dragged);
    }
}

[Serializable, NetSerializable]
public enum GulagTeleporterVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum GulagTeleporterState : byte
{
    Open,
    Closed,
    Occupied,
}

// ─── Консоль телепорта гулага (computer/prisoner/gulag_teleporter) ──────────

[Serializable, NetSerializable]
public enum GulagConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GulagConsoleState : BoundUserInterfaceState
{
    public string? PrisonerName;
    public string? CrimeStatus;
    public bool HasTeleporter;
    public string? TeleporterLocation;
    public bool TeleporterLocked;
    public bool TeleporterOpen;
    public bool HasBeacon;
    public string? BeaconLocation;
    public bool HasId;
    public string? IdName;
    public int Goal;
    public bool CanTeleport;
}

[Serializable, NetSerializable]
public enum GulagConsoleAction : byte
{
    ScanTeleporter,
    ScanBeacon,
    HandleId,
    ToggleOpen,
    ToggleLock,
    Teleport,
}

[Serializable, NetSerializable]
public sealed class GulagConsoleActionMessage(GulagConsoleAction action) : BoundUserInterfaceMessage
{
    public readonly GulagConsoleAction Action = action;
}

[Serializable, NetSerializable]
public sealed class GulagConsoleSetGoalMessage(int goal) : BoundUserInterfaceMessage
{
    public readonly int Goal = goal;
}

// ─── Консоль управления заключёнными (computer/prisoner/management) ─────────

[Serializable, NetSerializable]
public enum PrisonerManagementUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PrisonerImplantInfo
{
    public NetEntity Implant;
    public string Category = string.Empty;
    public List<string> InfoKeys = new();
    public List<string> InfoValues = new();
    public bool CanInject;
    public bool CanWarn;
}

[Serializable, NetSerializable]
public sealed class PrisonerManagementState : BoundUserInterfaceState
{
    public bool HasId;
    public string? IdName;
    public int Points;
    public int Goal;
    public List<PrisonerImplantInfo> Implants = new();
}

[Serializable, NetSerializable]
public enum PrisonerManagementAction : byte
{
    InsertId,
    EjectId,
    ResetId,
}

[Serializable, NetSerializable]
public sealed class PrisonerManagementActionMessage(PrisonerManagementAction action) : BoundUserInterfaceMessage
{
    public readonly PrisonerManagementAction Action = action;
}

[Serializable, NetSerializable]
public sealed class PrisonerManagementSetGoalMessage(int goal) : BoundUserInterfaceMessage
{
    public readonly int Goal = goal;
}

[Serializable, NetSerializable]
public enum PrisonerImplantAction : byte
{
    SelfDestruct,
    Inject,
    Warn,
}

[Serializable, NetSerializable]
public sealed class PrisonerImplantActionMessage(NetEntity implant, PrisonerImplantAction action, int amount, string? text)
    : BoundUserInterfaceMessage
{
    public readonly NetEntity Implant = implant;
    public readonly PrisonerImplantAction Action = action;
    public readonly int Amount = amount;
    public readonly string? Text = text;
}

// ─── Консоль сбора очков (labor_claim_console) ─────────────────────────────

[Serializable, NetSerializable]
public enum LaborClaimUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class LaborClaimState : BoundUserInterfaceState
{
    public int IdPoints;
    public string StatusInfo = string.Empty;
    public int UnclaimedPoints;
    public bool CanGoHome;
}

[Serializable, NetSerializable]
public enum LaborClaimAction : byte
{
    ClaimPoints,
    MoveShuttle,
}

[Serializable, NetSerializable]
public sealed class LaborClaimActionMessage(LaborClaimAction action) : BoundUserInterfaceMessage
{
    public readonly LaborClaimAction Action = action;
}

// ─── Станция возврата снаряжения (gulag_item_reclaimer) ────────────────────

[Serializable, NetSerializable]
public enum GulagReclaimerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GulagReclaimerState : BoundUserInterfaceState
{
    public List<NetEntity> Mobs = new();
    public List<string> Names = new();
    public bool CanReclaim;
}

[Serializable, NetSerializable]
public sealed class GulagReclaimerReleaseMessage(NetEntity mob) : BoundUserInterfaceMessage
{
    public readonly NetEntity Mob = mob;
}

[Serializable, NetSerializable]
public enum GulagReclaimerVisuals : byte
{
    Emagged,
}

// ─── Compli-o-nator и электростул ──────────────────────────────────────────

/// <summary>datum/action/item_action/halt.</summary>
public sealed partial class SecHailerHaltActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum ElectricChairVisuals : byte
{
    Shocking,
}

/// <summary>Побег из запертого телепорта гулага (container_resist_act).</summary>
[Serializable, NetSerializable]
public sealed partial class GulagBreakoutDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
