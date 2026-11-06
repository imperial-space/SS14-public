using System.Linq;
using Content.Server.Administration;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>/obj/machinery/computer/camera_advanced логова: свободный глаз над станцией.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardScryingConsoleComponent : Component
{
    [DataField]
    public EntProtoId Eye = "ImperialWizardScryingEye";

    [DataField]
    public EntProtoId ExitAction = "ImperialActionScryingExit";
}
