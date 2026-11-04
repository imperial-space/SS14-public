using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Legion;

/// <summary>Страж легиона: через 1.8 с стреляет кровавым импульсом и исчезает.</summary>
[RegisterComponent]
public sealed partial class LegionTurretComponent : Component
{
    [ViewVariables] public EntityUid? Owner;
}
