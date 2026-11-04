using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>Имплант путеискателя: помнит костюм.</summary>
[RegisterComponent]
public sealed partial class ImperialModPathfinderImplantComponent : Component
{
    [ViewVariables]
    public EntityUid? Suit;

    [ViewVariables]
    public TimeSpan? ArriveAt;
}
