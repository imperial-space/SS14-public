using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>stealth: снимается при получении урона.</summary>
[RegisterComponent]
public sealed partial class ImperialModCloakComponent : Component
{
    [ViewVariables]
    public EntityUid Module;
}
