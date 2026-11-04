using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Спавнер гост-роли глюка на месте «мутирующего» существа домена (setup_glitch → spawn_glitch).</summary>
[RegisterComponent]
public sealed partial class BitrunningGlitchSpawnerComponent : Component
{
    [ViewVariables]
    public EntityUid Server;

    [ViewVariables]
    public EntityUid Target;
}
