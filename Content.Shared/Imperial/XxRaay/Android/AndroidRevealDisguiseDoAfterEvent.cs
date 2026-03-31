using System;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// DoAfter снятия маскировки
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AndroidRevealDisguiseDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
/// DoAfter вырывания
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AndroidRevealEscapeDoAfterEvent : SimpleDoAfterEvent
{
}

