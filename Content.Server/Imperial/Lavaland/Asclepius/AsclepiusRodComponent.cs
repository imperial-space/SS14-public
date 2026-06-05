using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.Asclepius;

[RegisterComponent]
[Access(typeof(AsclepiusRodSystem))]
public sealed partial class AsclepiusRodComponent : Component
{
    [DataField]
    public string DormantState = "asclepius_dormant";

    [DataField]
    public string ActiveState = "asclepius_active";

    [DataField]
    public string DormantHeldPrefix = "asclepius_dormant";

    [DataField]
    public string ActiveHeldPrefix = "asclepius_active";

    [DataField]
    public bool OathTaken;

    [DataField]
    public float HealRange = 5f;

    [DataField("healPerTick")]
    public DamageSpecifier HealPerTick = new()
    {
        DamageDict = new()
        {
            { "Blunt", -0.45f },
            { "Slash", -0.45f },
            { "Piercing", -0.35f },
            { "Heat", -0.30f },
            { "Cold", -0.20f },
            { "Shock", -0.20f },
            { "Poison", -0.15f },
            { "Bloodloss", -0.60f }
        }
    };

    [DataField]
    public float HealIntervalSeconds = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextHealTime;
}
