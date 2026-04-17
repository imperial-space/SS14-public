using Content.Shared.Damage;
using Content.Shared.Imperial.TerrorSpider.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.TerrorSpider.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedTerrorSpiderKnightGuardSystem)), AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class TerrorSpiderKnightGuardComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public EntProtoId Action = "ActionTerrorSpiderKnightGuard";

    public EntityUid? ActionEntity;

    [DataField]
    [AutoNetworkedField]
    public float GuardDuration = 10f;

    [DataField]
    [AutoNetworkedField]
    public float GuardSpeedMultiplier = 0.5f;

    [DataField]
    [AutoNetworkedField]
    public float GuardMeleeDamage = 10f;

    [DataField]
    [AutoNetworkedField]
    public float BruteIncomingMultiplier = 0.4f;

    [DataField]
    [AutoNetworkedField]
    public float BurnIncomingMultiplier = 0.7f;

    [ViewVariables]
    [AutoNetworkedField]
    public bool IsGuarding;

    [ViewVariables]
    [AutoPausedField]
    [AutoNetworkedField]
    public TimeSpan GuardEndTime;

    [ViewVariables]
    public DamageSpecifier? CachedMeleeDamage;

    [ViewVariables]
    public DamageSpecifier? CachedPassiveDamage;

    [ViewVariables]
    public float CachedBruteModifier;

    [ViewVariables]
    public float CachedBurnModifier;
}
