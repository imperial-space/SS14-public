using System.Linq;
using Content.Server.Polymorph.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>charge: перезарядить предмет в руке или вернуть откаты тащимому магу.</summary>
[RegisterComponent]
public sealed partial class ImperialChargeSpellComponent : Component;
