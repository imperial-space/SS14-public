using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Медик видит раненых своей стороны и слышит их вызов.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaMedicSightComponent : Component;
