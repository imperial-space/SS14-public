using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>losebreath: сбитое дыхание после удара под дых.</summary>
[RegisterComponent]
public sealed partial class MartialLoseBreathComponent : Component
{
    /// <summary>Сколько жизненных тиков ещё не дышать.</summary>
    [ViewVariables]
    public int Ticks;

    [ViewVariables]
    public TimeSpan NextTick;
}
