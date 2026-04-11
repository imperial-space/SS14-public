using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderDestroyerComponent : Component
{
    [DataField]
    public EntProtoId EmpScreamAction = "ActionTerrorSpiderDestroyerEmpScream";

    [DataField]
    public EntityUid? EmpScreamActionEntity;

    [DataField]
    public EntProtoId FireBurstAction = "ActionTerrorSpiderDestroyerFireBurst";

    [DataField]
    public EntityUid? FireBurstActionEntity;

    [DataField]
    public float EmpScreamRange = 3.5f;

    [DataField]
    public float DeathEmpRange = 6.5f;

    [DataField]
    public float EmpScreamEnergyConsumption = 120000f;

    [DataField]
    public float EmpScreamDisableSeconds = 20f;

    [DataField]
    public SoundSpecifier? EmpUseSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");

    [DataField]
    public SoundSpecifier? EmpDeathSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");

    [ViewVariables]
    public bool DeathEmpTriggered;

    [DataField]
    public float FireBurstRadius = 3.5f;

    [DataField]
    public float FireStacksPerHit = 4f;

    [DataField]
    public float FireHeatDamagePerHit = 12f;
}
