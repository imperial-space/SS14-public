using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Узел сорняков (weeds/node): каждые 5–10 с даёт рост сорнякам в радиусе 3.</summary>
[RegisterComponent]
public sealed partial class XenoWeedNodeComponent : Component
{
    [DataField]
    public int Range = 3;

    [DataField]
    public TimeSpan MinGrowTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MaxGrowTime = TimeSpan.FromSeconds(10);

    [DataField]
    public List<EntProtoId> Weeds = new() { "ImperialXenoWeeds1", "ImperialXenoWeeds2", "ImperialXenoWeeds3" };

    [ViewVariables] public TimeSpan NextGrow;
}
