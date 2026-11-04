using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/obj/item/shield/changeling: блокирует половину ударов, пока не кончатся remaining_uses.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingShieldComponent : Component
{
    [DataField]
    public float BlockChance = 0.5f;

    [ViewVariables]
    public int RemainingUses;
}
