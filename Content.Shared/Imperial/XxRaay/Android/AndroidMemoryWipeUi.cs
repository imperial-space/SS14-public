using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Ключ UI окна уведомления об обнулении памяти
/// </summary>
[Serializable, NetSerializable]
public enum AndroidMemoryWipeUiKey : byte
{
    Key
}

/// <summary>
/// Состояние UI обнуления памяти
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidMemoryWipeBuiState : BoundUserInterfaceState
{
}

/// <summary>
/// Сообщение от клиента о подтверждении обнуления памяти
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidMemoryWipeAcknowledgeMessage : BoundUserInterfaceMessage
{
    public readonly bool Acknowledged;

    public AndroidMemoryWipeAcknowledgeMessage(bool acknowledged)
    {
        Acknowledged = acknowledged;
    }
}

