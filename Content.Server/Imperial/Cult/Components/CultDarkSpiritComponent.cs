using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Вознёсшийся тёмный дух (Ascend as a Dark Spirit).</summary>
[RegisterComponent]
public sealed partial class CultDarkSpiritComponent : Component
{
    [ViewVariables] public EntityUid? Body;
    [ViewVariables] public EntityUid? Rune;
    [ViewVariables] public List<EntityUid> Actions = new();
    [ViewVariables] public TimeSpan NextMark;
    [ViewVariables] public TimeSpan NextCheck;

    [DataField]
    public TimeSpan GhostMarkDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan MarkDelay = TimeSpan.FromSeconds(60);
}
