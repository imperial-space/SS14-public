using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Рисование руны Великого ритуала (4 секунды).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialGrandRuneDrawDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
