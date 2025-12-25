using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

[RegisterComponent]
public sealed partial class SmithArmorBaseComponent : Component
{
    [DataField] public DamageModifierSet Base = new();
    [DataField] public bool HasBase;
}

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class SmithQualityComponent : Component
{
    [DataField] [AutoNetworkedField]
    public bool Applied;

    [DataField] [AutoNetworkedField]
    public float Modifier = 1f;

    [DataField] [AutoNetworkedField]
    public ItemQuality Quality = ItemQuality.Default;
}
