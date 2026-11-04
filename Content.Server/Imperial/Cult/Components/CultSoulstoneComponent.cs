using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Камень душ (obj/item/soulstone).</summary>
[RegisterComponent]
public sealed partial class CultSoulstoneComponent : Component
{
    [DataField] public bool Spent;
    [DataField] public bool Purified;
    [ViewVariables] public EntityUid? Shade;
    [DataField] public string ContainerId = "soulstone_shade";
}
