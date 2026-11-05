using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

/// <summary>Разборка ящика отвёрткой (storagebox screwdriver_act).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialStorageBoxDisassembleDoAfterEvent : SimpleDoAfterEvent;
