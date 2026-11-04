using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/consuming: ест еду и делает печенье.</summary>
[RegisterComponent]
public sealed partial class SlimeConsumingComponent : Component
{
    [DataField]
    public float NutrimentRequired = 10f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    [DataField]
    public int Cookies = 5;

    [DataField]
    public EntProtoId? Cookie;

    [ViewVariables]
    public float NutrimentEaten;

    [ViewVariables]
    public TimeSpan LastProduced = TimeSpan.FromMinutes(-10);
}
