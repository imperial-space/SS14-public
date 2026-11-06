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

/// <summary>Тот, кто сейчас смотрит через консоль.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardScryingUserComponent : Component
{
    [ViewVariables]
    public EntityUid Eye;

    [ViewVariables]
    public EntityUid Console;

    [ViewVariables]
    public EntityUid? Action;

    [DataField]
    public float UpdateRange = 1.5f;
}
