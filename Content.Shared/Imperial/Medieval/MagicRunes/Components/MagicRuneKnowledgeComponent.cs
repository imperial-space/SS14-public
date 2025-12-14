using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Robust.Shared.GameStates;

//=========================================================================
// MagicRuneKnowledgeComponent.cs
//=========================================================================
// Purpose: Component for tracking player's known magic runes
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MagicRuneKnowledgeComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public HashSet<MagicRune> KnownRunes = new();

    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxRunesKnowledge = 4;
}
