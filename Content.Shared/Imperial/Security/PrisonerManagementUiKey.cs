using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Консоль управления заключёнными (computer/prisoner/management) ─────────

[Serializable, NetSerializable]
public enum PrisonerManagementUiKey : byte
{
    Key,
}
