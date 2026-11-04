using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Hierophant;

/// <summary>Временная стена арены: пропускает только своего иерофанта.</summary>
[RegisterComponent]
public sealed partial class HierophantWallComponent : Component
{
    [ViewVariables] public EntityUid? Caster;
}
