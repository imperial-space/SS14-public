using System;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// DoAfter-событие завершения обнуления памяти
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AndroidMemoryWipeDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
/// DoAfter-событие, когда цель пытается вырваться из захвата при обнулении памяти
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AndroidMemoryWipeEscapeDoAfterEvent : SimpleDoAfterEvent
{
}

