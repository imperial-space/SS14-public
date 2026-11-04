using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/reproductive: кормление кубиками обезьян даёт экстракты.</summary>
[RegisterComponent]
public sealed partial class SlimeReproductiveComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Extract;

    [DataField]
    public int FeedAmount = 3;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public int Cubes;

    [ViewVariables]
    public TimeSpan LastProduce;
}
