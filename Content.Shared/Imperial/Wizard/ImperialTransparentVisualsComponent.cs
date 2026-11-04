using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Перк «Прозрачность»: маг полупрозрачен.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialTransparentVisualsComponent : Component;
