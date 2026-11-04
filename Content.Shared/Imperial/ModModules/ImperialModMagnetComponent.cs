using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// magboot: носитель не теряет сцепление в невесомости, пока стоит на гриде.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModMagnetComponent : Component;
