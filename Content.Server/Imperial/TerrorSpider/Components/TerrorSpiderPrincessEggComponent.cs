using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderPrincessEggComponent : Component
{
    [DataField]
    public EntityUid? Princess;

    [DataField]
    public bool TierTwo;

    [DataField]
    public float HatchDelay = 240f;

    [DataField]
    public TimeSpan HatchAt = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> TierOnePrototypes = new()
    {
        "MobRusarSpiderAngrys",
        "MobDronSpiderAngrys",
        "MobsogladatelSpiderAngrys",
        "MobGiantHealerSpiderAngry"
    };

    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> TierTwoPrototypes = new()
    {
        "MobGiantreaperSpiderAngry",
        "MobGiantWidowSpiderAngry",
        "MobGiantGuardianSpiderAngry",
        "MobGiantDestroyerSpiderAngry"
    };

    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> TierTwoElitePrototypes = new()
    {
        "MobGiantWidowSpiderAngry",
        "MobGiantGuardianSpiderAngry",
        "MobGiantDestroyerSpiderAngry"
    };

    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> TierTwoUnlimitedPrototypes = new()
    {
        "MobGiantreaperSpiderAngry"
    };
}
