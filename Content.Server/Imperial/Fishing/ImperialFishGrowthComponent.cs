using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Cargo;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>Рост рыбы: в существо, в другой вид или откладывание икры. У одной рыбы может быть несколько ростов.</summary>
[RegisterComponent]
public sealed partial class ImperialFishGrowthComponent : Component
{
    [ViewVariables]
    public List<ImperialFishGrowth> Growths = new();
}
