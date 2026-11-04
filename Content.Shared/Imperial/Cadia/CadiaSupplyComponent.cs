using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Склад: выдаёт по одному предмету рукой. Ящик боеприпасов выдаёт обойму к оружию в руках.</summary>
[RegisterComponent]
public sealed partial class CadiaSupplyComponent : Component
{
    [DataField]
    public int Charges = 20;

    /// <summary>Что выдавать; пусто — обойму к оружию в руках.</summary>
    [DataField]
    public List<EntProtoId> Items = new();

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/rustle1.ogg");

    [DataField]
    public LocId EmptyPopup = "cadia-supply-empty";

    [DataField]
    public bool DeleteWhenEmpty;
}
