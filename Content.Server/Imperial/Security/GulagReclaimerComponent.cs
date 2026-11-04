using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>gulag_item_reclaimer.</summary>
[RegisterComponent]
public sealed partial class GulagReclaimerComponent : Component
{
    public const string ContainerId = "gulag-reclaimer-items";

    [ViewVariables]
    public Container Items = default!;

    /// <summary>stored_items: чьи вещи лежат внутри.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, List<EntityUid>> Stored = new();

    [ViewVariables]
    public bool Emagged;
}
