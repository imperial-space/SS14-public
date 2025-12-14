using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Robust.Shared.GameStates;

//=========================================================================
// MagicStoneComponent.cs
//=========================================================================
// Purpose: Component for magic stones containing single runes for learning
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public partial class MagicStoneComponent : Component
{
    [DataField, AutoNetworkedField]
    public MagicRune Rune = MagicRune.Kael;
}
