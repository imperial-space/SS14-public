using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>Жертва, высушенная генокрадом (Drain): её нельзя поглотить снова.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialChangelingAbsorbedComponent : Component;
