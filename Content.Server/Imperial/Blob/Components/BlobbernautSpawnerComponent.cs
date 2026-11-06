using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Blob.Components;

/// <summary>Опрос призраков на блоббернаута (pick_blobbernaut_candidate).</summary>
[RegisterComponent]
public sealed partial class BlobbernautSpawnerComponent : Component
{
    [ViewVariables] public EntityUid? Overmind;
    [ViewVariables] public EntityUid? Factory;
    [ViewVariables] public TimeSpan Expire;
}
