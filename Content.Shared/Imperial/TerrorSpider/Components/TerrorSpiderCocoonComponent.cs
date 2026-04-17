using Content.Shared.Imperial.TerrorSpider.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.TerrorSpider.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedTerrorSpiderCocoonSystem)), AutoGenerateComponentState]
public sealed partial class TerrorSpiderCocoonComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    [AutoNetworkedField]
    public EntProtoId Action = "ActionTerrorSpiderCocoon";

    public EntityUid? ActionEntity;

    [DataField]
    [AutoNetworkedField]
    public EntProtoId CocoonPrototype = "TerrorSpiderCocoon";

    [DataField]
    [AutoNetworkedField]
    public float CocoonDelay = 4f;
}
