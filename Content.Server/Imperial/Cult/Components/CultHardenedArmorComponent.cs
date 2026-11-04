using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Nar'Sien hardened armor: раны не-культистам, экзорцизм библией.</summary>
[RegisterComponent]
public sealed partial class CultHardenedArmorComponent : Component
{
    [ViewVariables] public EntityUid? Wearer;
    [ViewVariables] public TimeSpan NextTick;
}
