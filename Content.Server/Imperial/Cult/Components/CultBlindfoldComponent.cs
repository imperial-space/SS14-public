using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Zealot's blindfold: жжёт глаза не-культистам.</summary>
[RegisterComponent]
public sealed partial class CultBlindfoldComponent : Component
{
    [ViewVariables] public EntityUid? Wearer;
    [ViewVariables] public TimeSpan NextTick;

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(2);
}
