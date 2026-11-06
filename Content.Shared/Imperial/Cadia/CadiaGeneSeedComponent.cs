using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Геносемя: своё даёт ещё одно место десантника в следующей волне, Хаосу — силу варпа.</summary>
[RegisterComponent]
public sealed partial class CadiaGeneSeedComponent : Component
{
    [DataField]
    public int WarpValue = 15;
}
