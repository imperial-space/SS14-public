using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BSA;

[NetSerializable, Serializable]
public enum BSAConsoleUiKey : byte
{
    Key,
}

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

/// <summary>«Complete Deployment»: собрать пушку из привязанных частей.</summary>
[NetSerializable, Serializable]
public sealed class BSABuildMessage : BoundUserInterfaceMessage;

/// <summary>«FIRE».</summary>
[NetSerializable, Serializable]
public sealed class BSAFireMessage : BoundUserInterfaceMessage;

/// <summary>«Recalibrate»: навести орудие на маяк.</summary>
[NetSerializable, Serializable]
public sealed class BSASetTargetMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}
