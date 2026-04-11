using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BSA;

[NetSerializable, Serializable]
public enum BSAControlBoxUiKey : byte
{
    Key,
}

/// <summary>
/// Сообщение от клиента: выбрать маяк-цель.
/// </summary>
[NetSerializable, Serializable]
public sealed class BSASelectTargetMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity? Target;

    public BSASelectTargetMessage(NetEntity? target)
    {
        Target = target;
    }
}

/// <summary>
/// Сообщение от клиента: открыть огонь.
/// </summary>
[NetSerializable, Serializable]
public sealed class BSAFireMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Сообщение от клиента: сканировать и собрать части.
/// </summary>
[NetSerializable, Serializable]
public sealed class BSAScanMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Состояние UI артиллерии, отправляемое клиенту.
/// </summary>
[NetSerializable, Serializable]
public sealed class BSAUIState : BoundUserInterfaceState
{
    public readonly bool Assembled;
    public readonly bool Powered;
    public readonly bool FrontPartExists;
    public readonly bool BackPartExists;
    public readonly bool CanFire;
    public readonly TimeSpan? NextFire;
    public readonly NetEntity? SelectedTarget;
    public readonly List<(NetEntity Uid, string Name)> AvailableBeacons;

    public BSAUIState(
        bool assembled,
        bool powered,
        bool frontPartExists,
        bool backPartExists,
        bool canFire,
        TimeSpan? nextFire,
        NetEntity? selectedTarget,
        List<(NetEntity, string)> availableBeacons)
    {
        Assembled = assembled;
        Powered = powered;
        FrontPartExists = frontPartExists;
        BackPartExists = backPartExists;
        CanFire = canFire;
        NextFire = nextFire;
        SelectedTarget = selectedTarget;
        AvailableBeacons = availableBeacons;
    }
}
