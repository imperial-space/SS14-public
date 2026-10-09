using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Общие свойства всех форм святого оружия (nullrod_core из SS13):
/// удар по руне культа или еретика стирает её, а по «духам» оружие бьёт сильнее.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodCoreComponent : Component
{
    /// <summary>Что выкрикивает владелец, стирая руну.</summary>
    [DataField]
    public LocId RuneRemoveLine = "null-rod-rune-remove-line";

    /// <summary>Дополнительный урон по отдельным видам существ.</summary>
    [DataField]
    public List<NullRodBane> Banes = new();
}

[DataDefinition]
public sealed partial class NullRodBane
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}
