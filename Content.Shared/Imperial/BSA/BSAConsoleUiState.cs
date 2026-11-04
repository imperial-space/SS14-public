using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BSA;

/// <summary>
/// Состояние консоли управления блюспейс-артиллерией (tgui BluespaceArtillery из SS13).
/// </summary>
[NetSerializable, Serializable]
public sealed class BSAConsoleUiState(
    bool connected,
    string? notice,
    bool unlocked,
    string? target,
    bool ready,
    List<(NetEntity Uid, string Name)> targets) : BoundUserInterfaceState
{
    public readonly bool Connected = connected;
    public readonly string? Notice = notice;
    public readonly bool Unlocked = unlocked;
    public readonly string? Target = target;
    public readonly bool Ready = ready;

    /// <summary>Маяки (WarpPoint), на которые можно навести орудие.</summary>
    public readonly List<(NetEntity Uid, string Name)> Targets = targets;
}
