using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Очки ночного видения TG (glasses/night): выдают носителю ImperialNightVision.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialGrantNightVisionComponent : Component
{
    [ViewVariables]
    public bool Granted;
}
