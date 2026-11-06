using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/regenerative: лечит, как регенеративное ядро легиона.</summary>
[RegisterComponent]
public sealed partial class SlimeRegenerativeComponent : Component
{
    /// <summary>regenerative/bluespace: место создания.</summary>
    [ViewVariables]
    public EntityCoordinates? Origin;

    [DataField]
    public TimeSpan RegenerativeAfterSlimeOilFlashDurationDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan RegenerativeAfterSlimeAdamantineDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan RegenerativeAfterSlimeRainbowDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public float RegenerativeAfterSlimePurpleAmount = 10f;

    [DataField]
    public FixedPoint2 RegenerativeAfterSlimeBlueQuantity = 15;

    [DataField]
    public float RegenerativeAfterSlimeRedAmount = 3f;

    [DataField]
    public float RegenerativeAfterSlimeGreenAmount = 5f;

    [DataField]
    public float RegenerativeAfterSlimePinkAmount = 4f;

    [DataField]
    public float RegenerativeAfterSlimeOilRange = 7f;
}
