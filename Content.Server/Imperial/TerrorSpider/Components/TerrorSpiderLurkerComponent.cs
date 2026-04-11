using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderLurkerComponent : Component
{
    [DataField]
    public EntProtoId StealthAction = "ActionTerrorSpiderLurkerStealth";

    [DataField]
    public EntityUid? StealthActionEntity;

    [DataField]
    public float StealthDuration = 8f;

    [DataField]
    public float BaseMeleeDamage = 15f;

    [DataField]
    public float WebBuffMeleeDamage = 45f;

    [DataField]
    public float WebBuffStaminaDamage = 45f;

    [DataField]
    public float MinStealthVisibility = -10f;

    [ViewVariables]
    public bool ActionStealthActive;

    [ViewVariables]
    public TimeSpan ActionStealthEndTime;

    [ViewVariables]
    public int WebContacts;
}
