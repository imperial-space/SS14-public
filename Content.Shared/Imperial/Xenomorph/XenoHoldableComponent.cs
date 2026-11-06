using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>
/// Предмет, который ксеноморф может взять в руки (alien/xeno_allowed_items для itempicky):
/// лицехват, королевский паразит, игрушечный ксено.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoHoldableComponent : Component;
