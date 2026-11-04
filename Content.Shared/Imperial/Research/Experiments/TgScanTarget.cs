using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>Цель сканирования: подходящие прототипы и/или вайтлист, сколько очков даёт.</summary>
[DataDefinition]
public sealed partial class TgScanTarget
{
    [DataField]
    public List<EntProtoId> Prototypes = new();

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public int Points = 1;
}
