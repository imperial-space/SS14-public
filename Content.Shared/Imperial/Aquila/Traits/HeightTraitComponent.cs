using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared.Imperial.Aquila.Traits;

[RegisterComponent]
public sealed partial class HeightTraitComponent : Component
{
    [DataField(required: true)]
    public float Modifier = 1f;

    public Vector2? BaseScale;

    public float LastAppliedModifier = float.NaN;
}
