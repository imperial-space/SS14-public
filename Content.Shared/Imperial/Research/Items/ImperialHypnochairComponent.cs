using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Кресло усиленного допроса (hypnochair): долгий сеанс вводит пристёгнутого в гипнотический транс.</summary>
[RegisterComponent]
public sealed partial class ImperialHypnochairComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30f);
}
