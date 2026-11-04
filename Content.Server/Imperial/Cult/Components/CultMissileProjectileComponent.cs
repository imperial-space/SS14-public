using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Малая магическая ракета ремесленника.</summary>
[RegisterComponent]
public sealed partial class CultMissileProjectileComponent : Component
{
    [ViewVariables] public EntityUid? Target;
    [DataField] public float Speed = 4f;
}
