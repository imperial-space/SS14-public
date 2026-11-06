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

/// <summary>Один рост рыбы (/datum/component/fish_growth).</summary>
public sealed class ImperialFishGrowth
{
    /// <summary>Прототип сущности или эволюция ("evolution:Id").</summary>
    public string Result = string.Empty;

    /// <summary>Прирост за секунду (100 / время роста).</summary>
    public float Rate;

    public float Maturation;
    public bool UseDropLoc = true;
    public bool DelOnGrow = true;
    public bool InheritName = true;

    public bool IsEvolution => Result.StartsWith(ImperialSpecialFishSystem.EvolutionPrefix);
}
