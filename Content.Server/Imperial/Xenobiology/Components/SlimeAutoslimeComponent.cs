using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>obj/item/autoslime: самоподдерживающийся экстракт, реакция выбирается при использовании.</summary>
[RegisterComponent]
public sealed partial class SlimeAutoslimeComponent : Component
{
    [DataField]
    public EntProtoId? Extract;
}
