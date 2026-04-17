using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderHealerComponent : Component
{
    [DataField]
    public EntProtoId PulseAction = "ActionTerrorSpiderHealerPulse";

    [DataField]
    public EntityUid? PulseActionEntity;

    [DataField]
    public EntProtoId EggRusarAction = "ActionTerrorSpiderHealerLayEggRusar";

    [DataField]
    public EntityUid? EggRusarActionEntity;

    [DataField]
    public EntProtoId EggDronAction = "ActionTerrorSpiderHealerLayEggDron";

    [DataField]
    public EntityUid? EggDronActionEntity;

    [DataField]
    public EntProtoId EggLurkerAction = "ActionTerrorSpiderHealerLayEggLurker";

    [DataField]
    public EntityUid? EggLurkerActionEntity;

    [DataField]
    public EntProtoId EggHealerAction = "ActionTerrorSpiderHealerLayEggHealer";

    [DataField]
    public EntityUid? EggHealerActionEntity;

    [DataField]
    public int SatietyLevel = 1;

    [DataField]
    public int MaxSatietyLevel = 10;

    [DataField]
    public int EggRequiredSatiety = 3;

    [DataField]
    public int CocoonSatietyGain = 1;

    [DataField]
    public float TouchHealLevel1 = 2f;

    [DataField]
    public float TouchHealLevel2 = 4f;

    [DataField]
    public float TouchHealLevel3 = 8f;

    [DataField]
    public float PulseHealAmount = 20f;

    [DataField]
    public float PulseRange = 13f;

    [DataField]
    public float TouchHealCooldown = 1f;

    [ViewVariables]
    public TimeSpan NextTouchHealTime = TimeSpan.Zero;
}
