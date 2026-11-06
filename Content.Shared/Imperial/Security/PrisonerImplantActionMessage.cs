using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class PrisonerImplantActionMessage(NetEntity implant, PrisonerImplantAction action, int amount, string? text)
    : BoundUserInterfaceMessage
{
    public readonly NetEntity Implant = implant;
    public readonly PrisonerImplantAction Action = action;
    public readonly int Amount = amount;
    public readonly string? Text = text;
}
