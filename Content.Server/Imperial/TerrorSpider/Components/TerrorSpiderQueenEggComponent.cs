using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderQueenEggComponent : Component
{
    [DataField]
    public EntityUid? Queen;

    [DataField]
    public float HatchDelay = 240f;

    [DataField]
    public TimeSpan HatchAt = TimeSpan.Zero;

    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string SpawnPrototype = string.Empty;

    [DataField]
    public string? RoyalCooldownKey;
}
