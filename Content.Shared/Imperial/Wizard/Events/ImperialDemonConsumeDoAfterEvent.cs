using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Одна из трёх стадий пожирания жертвы в крови.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialDemonConsumeDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
