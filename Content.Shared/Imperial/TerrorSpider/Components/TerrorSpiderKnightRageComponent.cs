using Content.Shared.Imperial.TerrorSpider.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.TerrorSpider.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedTerrorSpiderKnightRageSystem)), AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class TerrorSpiderKnightRageComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public EntProtoId Action = "ActionTerrorSpiderKnightRage";

    public EntityUid? ActionEntity;

    [DataField]
    [AutoNetworkedField]
    public float RageDuration = 10f;

    [DataField]
    [AutoNetworkedField]
    public float EnragedSpeedMultiplier = 1.33f;

    [DataField]
    [AutoNetworkedField]
    public float EnragedMeleeDamage = 30f;

    [DataField]
    [AutoNetworkedField]
    public float BruteIncomingMultiplier = 0.8f;

    [DataField]
    [AutoNetworkedField]
    public float BurnIncomingMultiplier = 1.2f;

    [ViewVariables]
    [AutoNetworkedField]
    public bool IsEnraged;

    [ViewVariables]
    [AutoPausedField]
    public TimeSpan RageEndTime;

    [ViewVariables]
    public DamageSpecifier? CachedMeleeDamage;

    [ViewVariables]
    public DamageSpecifier? CachedPassiveDamage;

    [ViewVariables]
    public float CachedBruteModifier;

    [ViewVariables]
    public float CachedBurnModifier;

    [ViewVariables]
    public bool HasCachedArmor;
}
