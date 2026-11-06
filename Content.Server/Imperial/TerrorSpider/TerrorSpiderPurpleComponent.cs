using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>Фиолетовый: страж королевы, вырождается вдали от неё.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderPurpleComponent : Component
{
    [DataField]
    public float MaxQueenRange = 20;

    [ViewVariables]
    public bool QueenVisible = true;

    [ViewVariables]
    public int CyclesNoQueen;

    [ViewVariables]
    public int LifeTicks;
}
