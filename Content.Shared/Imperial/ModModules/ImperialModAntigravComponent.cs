using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>antigrav: носитель парит (forced_gravity 0).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModAntigravComponent : Component;
