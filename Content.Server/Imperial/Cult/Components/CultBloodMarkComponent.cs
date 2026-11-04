using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Отметка «кровавой метки» над целью.</summary>
[RegisterComponent]
public sealed partial class CultBloodMarkComponent : Component
{
    [ViewVariables] public EntityUid? Target;
}
