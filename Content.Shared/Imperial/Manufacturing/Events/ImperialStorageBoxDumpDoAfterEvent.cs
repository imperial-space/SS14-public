using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

/// <summary>Высыпать содержимое ящика рукой (storagebox attack_hand).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialStorageBoxDumpDoAfterEvent : SimpleDoAfterEvent;
