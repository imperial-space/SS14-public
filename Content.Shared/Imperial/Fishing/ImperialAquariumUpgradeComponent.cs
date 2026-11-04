using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Улучшение аквариума (/obj/item/aquarium_upgrade): превращает один вид аквариума в другой.</summary>
[RegisterComponent]
public sealed partial class ImperialAquariumUpgradeComponent : Component
{
    [DataField(required: true)]
    public string From = default!;

    [DataField(required: true)]
    public string To = default!;
}
