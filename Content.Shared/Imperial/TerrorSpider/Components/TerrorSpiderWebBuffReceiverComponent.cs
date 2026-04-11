using Content.Shared.Imperial.TerrorSpider.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.TerrorSpider.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedTerrorSpiderWebBuffSystem)), AutoGenerateComponentState]
public sealed partial class TerrorSpiderWebBuffReceiverComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public int WebContacts;

    [ViewVariables]
    public TimeSpan NextRegenTick;

    [DataField]
    [AutoNetworkedField]
    public float RegenPerTick = 3f;

    [DataField]
    [AutoNetworkedField]
    public float RegenInterval = 2f;

    [DataField]
    [AutoNetworkedField]
    public float SpeedMultiplier = 1f;
}
