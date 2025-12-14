using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

//=========================================================================
// MagicScrollUIData.cs
//=========================================================================
// Purpose: Data structures for magic scroll user interface communication
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Data;

[NetSerializable, Serializable]
public enum MagicScrollUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MagicScrollBoundUserInterfaceState(int scrollPower, List<MagicRune> encryptedRunes, HashSet<MagicRune> decodedRunes, HashSet<MagicRune> knownRunes, int playerIntelligence, int gridSize, int totalMines) : BoundUserInterfaceState
{
    public int ScrollPower = scrollPower;
    public List<MagicRune> EncryptedRunes = encryptedRunes;
    public HashSet<MagicRune> DecodedRunes = decodedRunes;
    public HashSet<MagicRune> KnownRunes = knownRunes;
    public int PlayerIntelligence = playerIntelligence;
    public int GridSize = gridSize;
    public int TotalMines = totalMines;
}

[Serializable, NetSerializable]
public sealed class MagicScrollRuneUnlockedMessage(MagicRune rune) : BoundUserInterfaceMessage
{
    public MagicRune Rune = rune;
}

[Serializable, NetSerializable]
public sealed class MagicScrollExplosionMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class BarrierSuicideDoAfterEvent : SimpleDoAfterEvent
{
}
