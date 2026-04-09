using Content.Shared.Imperial.TerrorSpider.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.TerrorSpider.Components;

/// <summary>
/// Modifies incoming damage for terror spiders by damage group.
/// BruteModifier affects Blunt, Slash, Piercing (and the Brute group itself).
/// BurnModifier affects Heat, Shock, Cold, Caustic (and the Burn group itself).
/// Values below 1 reduce damage, above 1 increase it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedTerrorSpiderArmorSystem), typeof(SharedTerrorSpiderKnightGuardSystem), typeof(SharedTerrorSpiderKnightRageSystem))]
public sealed partial class TerrorSpiderArmorComponent : Component
{
    /// <summary>
    /// Multiplier for Brute damage group (Blunt, Slash, Piercing).
    /// Default 1.0 = no modification.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float BruteModifier = 1f;

    /// <summary>
    /// Multiplier for Burn damage group (Heat, Shock, Cold, Caustic).
    /// Default 1.0 = no modification.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float BurnModifier = 1f;
}
