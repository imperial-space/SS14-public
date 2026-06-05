using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Lavaland.Weaver;

[RegisterComponent, NetworkedComponent]
public sealed partial class WeaverComponent : Component
{
    [DataField] public float RageThreshold = 0.333f;
    [DataField] public bool IsEnraged;

    [DataField] public float NormalDamageMin = 13f;
    [DataField] public float NormalDamageMax = 16f;
    [DataField] public float RageDamageMin = 15f;
    [DataField] public float RageDamageMax = 20f;

    [DataField] public ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> NormalReagent = "WeaverSpore";
    [DataField] public float NormalReagentAmount = 5f;
    [DataField] public ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> RageReagent = "WeaverVenom";
    [DataField] public float RageReagentAmount = 6f;

    [DataField] public float RageSpeedBonus = 0.6f;

    [DataField] public float CorpseEatRange = 2f;
    [DataField] public float CorpseEatHeal = 35f;
    [DataField] public TimeSpan CorpseEatCooldown = TimeSpan.FromSeconds(6);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextEatTime;
}
