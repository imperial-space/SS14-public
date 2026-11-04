using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Снаряд макропушки: вкладывается в консоль батареи.</summary>
[RegisterComponent]
public sealed partial class CadiaMacroShellComponent : Component
{
    [DataField]
    public int Rounds = 1;
}
