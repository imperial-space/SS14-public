using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Консоль сбора очков (labor_claim_console) ─────────────────────────────

[Serializable, NetSerializable]
public enum LaborClaimUiKey : byte
{
    Key,
}
