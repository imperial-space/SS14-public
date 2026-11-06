using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

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
