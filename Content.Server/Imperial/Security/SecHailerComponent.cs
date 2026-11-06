using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>clothing/mask/gas/sechailer.</summary>
[RegisterComponent]
public sealed partial class SecHailerComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSecHailerHalt";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>aggressiveness.</summary>
    [DataField]
    public HailerAggression Aggression = HailerAggression.BadCop;

    /// <summary>Можно ли крутить ограничитель отвёрткой (у маски спецназа нельзя).</summary>
    [DataField]
    public bool Adjustable = true;

    [ViewVariables]
    public bool BrokenHailer;

    [ViewVariables]
    public bool Emagged;

    [ViewVariables]
    public int RecentUses;

    [ViewVariables]
    public TimeSpan? OveruseResetAt;

    [ViewVariables]
    public TimeSpan NextPhrase;
}
