using Content.Shared.Imperial.Medieval.MagicRunes.Data;
using Robust.Shared.GameStates;

//=========================================================================
// MagicScrollComponent.cs
//=========================================================================
// Purpose: Component for magic scrolls with encrypted runes
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MagicScrollComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int BasicPower = 2;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxRunes = 4;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int Power;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool Bad;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<MagicRune> EncryptedRunes = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<MagicRune> DecodedRunes = new();

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int GridSize = 7;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int TotalMines = 7;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int PointsPerDecodedRune = 8;
}

