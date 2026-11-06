using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Космическая печь (space_bubble): держит пригодный воздух вокруг себя, пока включена.</summary>
[RegisterComponent]
public sealed partial class ImperialSpaceFurnaceComponent : Component
{
    [DataField]
    public int Radius = 2;

    [DataField]
    public bool Enabled;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(2);
}
