using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

/// <summary>Мультитул запомнил генератор щита (buffer).</summary>
[RegisterComponent]
public sealed partial class ImperialModularShieldBufferComponent : Component
{
    [ViewVariables]
    public EntityUid Generator;
}
