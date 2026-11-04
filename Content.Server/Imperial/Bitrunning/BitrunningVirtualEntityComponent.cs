using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Существо внутри домена (virtual_entity / кандидат в глюки).</summary>
[RegisterComponent]
public sealed partial class BitrunningVirtualEntityComponent : Component
{
    [ViewVariables]
    public EntityUid? Server;

    /// <summary>make_virtual_megafauna: со смертью выпадает зашифрованный ящик.</summary>
    [ViewVariables]
    public bool Megafauna;
}
